using AwesomeAssertions;
using TrainingHub.Shared;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.Domain;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Infrastructure.Outbox;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// Proves the transactional half of the outbox's name: a committed change carries its integration
/// events with it, and a failed save takes them down with it.
/// </summary>
/// <remarks>
/// <para>
/// The second half is the one worth a hard look. It cannot be shown over HTTP: a stale
/// <c>If-Match</c> is refused by the version pre-check before the aggregate is ever edited, so no
/// domain event is raised and there is nothing for the outbox to lose. The case that matters is
/// the race that pre-check exists to narrow but cannot close — two readers loaded the same row,
/// both passed, the slower writer hits the <c>rowversion</c> guard inside <c>SaveChanges</c>. By
/// then the domain events have been dispatched and the outbox row staged; the save fails; ADR 0002
/// promises the row died with it. Driving the repositories through two scopes reproduces that
/// exact interleaving deterministically.
/// </para>
/// <para>
/// Read directly from <c>TrainingContext</c> rather than over HTTP because the outbox has no
/// endpoint, deliberately: its only reader will be the delivery worker.
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture — one per host, since the wiring under test is
/// each host's own.</typeparam>
public abstract class OutboxTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IServiceScopeSource, IHttpClientSource, IServerErrorSource
{
    /// <summary>
    /// Registering a trainer, commits the trainer-created fact, into the outbox.
    /// </summary>
    [Fact]
    public async Task RegisteringATrainer_CommitsTheTrainerCreatedFact_IntoTheOutbox()
    {
        var request = AuthHelper.CreateUniqueRegisterRequest();

        var response = await AuthHelper.RegisterAsync(Factory.CreateClient(), request);
        response.EnsureSuccessStatusCode();

        using var scope = Factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrainingContext>();

        // Nothing here asserts on the delivery columns: the worker polls while this test runs, and
        // whether the row is still owed or already delivered is its business — the envelope and the
        // payload are what committing the fact promised.
        var message = (await context.Set<OutboxMessage>().ToListAsync())
            .Should().ContainSingle(m => m.Name == "TrainerCreated").Subject;

        message.Version.Should().Be(1);

        var fact = IntegrationEventSerializer.Deserialize(message.Name, message.Version, message.Payload)
            .Should().BeOfType<TrainerCreatedIntegrationEvent>().Subject;
        fact.ContactEmail.Should().Be(request.Email);
        fact.Firstname.Should().Be(request.Firstname);
        fact.Lastname.Should().Be(request.Lastname);
    }

    /// <summary>
    /// The worker, delivers the committed fact, and stamps the envelope.
    /// </summary>
    [Fact]
    public async Task TheWorker_DeliversTheCommittedFact_AndStampsTheEnvelope()
    {
        var request = AuthHelper.CreateUniqueRegisterRequest();

        var response = await AuthHelper.RegisterAsync(Factory.CreateClient(), request);
        response.EnsureSuccessStatusCode();

        var delivered = await WaitForMessageAsync(
            "TrainerCreated",
            message => message.ProcessedOnUtc is not null);

        delivered.ClaimedBy.Should().NotBeNull("a delivery starts with a claim, and the claim leaves provenance");
        delivered.Attempts.Should().Be(0, "a delivery that succeeds first time never spent the retry budget");
        delivered.Error.Should().BeNull();
    }

    /// <summary>
    /// A message nobody can read, spends its budget, and is left poisoned.
    /// </summary>
    /// <remarks>
    /// The row is planted with a wire name the registry does not know, so every delivery attempt
    /// fails at deserialization. The suite runs with an attempt budget of two: the worker must try
    /// twice, record why, then leave the row alone — and keep delivering everyone else, which the
    /// second half of the test proves with an ordinary registration.
    /// </remarks>
    [Fact]
    public async Task AMessageNobodyCanRead_SpendsItsBudget_AndIsLeftPoisoned()
    {
        var messageId = Guid.CreateVersion7();

        using (var scope = Factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrainingContext>();
            context.Set<OutboxMessage>().Add(new OutboxMessage(
                messageId,
                "NobodyKnowsThisName",
                1,
                "{}",
                DateTime.UtcNow));
            await context.SaveChangesAsync();
        }

        var poisoned = await WaitForMessageAsync(
            "NobodyKnowsThisName",
            message => message.Attempts >= 2);

        poisoned.ProcessedOnUtc.Should().BeNull("a message that cannot be read is never marked delivered");
        poisoned.Error.Should().Contain("No integration event is registered");
        poisoned.NextAttemptOnUtc.Should().NotBeNull(
            "every failed attempt books the next one — the schedule was written before the budget ran out");

        // The transition was announced: one Error line, naming the message, reached the host's
        // sinks — the smallest dead-letter surface ADR 0025 deferred, delivered by ADR 0033.
        Factory.ServerErrors.Should().Contain(
            error => error.Contains(messageId.ToString(), StringComparison.Ordinal),
            "poisoning is the moment the system gives up on a committed fact, and it must say so");

        // The poison did not stop the line: a fact committed afterwards still gets delivered.
        var request = AuthHelper.CreateUniqueRegisterRequest();
        (await AuthHelper.RegisterAsync(Factory.CreateClient(), request)).EnsureSuccessStatusCode();
        await WaitForMessageAsync("TrainerCreated", message => message.ProcessedOnUtc is not null);
    }

    /// <summary>
    /// A delivered message, outlives its retention, then is swept.
    /// </summary>
    /// <remarks>
    /// Three rows tell the boundary apart: one delivered long before the suite's retention window,
    /// one delivered just now, one unreadable and never delivered. The sweep must take exactly the
    /// first — delivered history is trimmed, fresh history is kept, and poison is an operator's
    /// evidence that nothing may delete (ADR 0033).
    /// </remarks>
    [Fact]
    public async Task ADeliveredMessage_OutlivesItsRetention_ThenIsSwept()
    {
        using (var scope = Factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TrainingContext>();

            var stale = new OutboxMessage(
                Guid.CreateVersion7(), "DeliveredLongAgo", 1, "{}", DateTime.UtcNow.AddMinutes(-11));
            stale.MarkProcessed(DateTime.UtcNow.AddMinutes(-10));

            var fresh = new OutboxMessage(
                Guid.CreateVersion7(), "DeliveredJustNow", 1, "{}", DateTime.UtcNow);
            fresh.MarkProcessed(DateTime.UtcNow);

            context.Set<OutboxMessage>().AddRange(
                stale,
                fresh,
                new OutboxMessage(Guid.CreateVersion7(), "NobodyKnowsThisOneEither", 1, "{}", DateTime.UtcNow));

            await context.SaveChangesAsync();
        }

        await WaitUntilGoneAsync("DeliveredLongAgo");

        using var assertScope = Factory.CreateScope();
        var messages = await assertScope.ServiceProvider.GetRequiredService<TrainingContext>()
            .Set<OutboxMessage>().ToListAsync();

        messages.Should().Contain(
            message => message.Name == "DeliveredJustNow",
            "a delivered message inside its retention is history an operator may still want");
        messages.Should().Contain(
            message => message.Name == "NobodyKnowsThisOneEither",
            "an undelivered message is never swept — poison waits for an operator, not for a broom");
    }

    /// <summary>
    /// A failed save, takes its outbox row down with it.
    /// </summary>
    [Fact]
    public async Task AFailedSave_TakesItsOutboxRowDownWithIt()
    {
        var trainerId = await SeedTrainerAsync();

        // The loser of the race reads first: from here on, scope A holds a trainer whose
        // rowversion is about to go stale.
        using var scopeA = Factory.CreateScope();
        var staleTrainers = scopeA.ServiceProvider.GetRequiredService<ITrainerRepository>();
        var staleTrainer = await staleTrainers.GetByIdAsync(trainerId);
        staleTrainer.Should().NotBeNull();

        // The winner edits and commits: one TrainerContactEmailChanged fact lands with it.
        using (var scopeB = Factory.CreateScope())
        {
            var trainers = scopeB.ServiceProvider.GetRequiredService<ITrainerRepository>();
            var unitOfWork = scopeB.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var trainer = await trainers.GetByIdAsync(trainerId);
            trainer!.Edit(trainer.Name, Required(Email.Create("committed@example.com")), trainer.Bio);
            trainers.Update(trainer);

            await unitOfWork.SaveChangesAsync();
        }

        // The loser edits the stale instance. The domain event is raised, the interceptor
        // dispatches it, the handler stages a second fact — and the rowversion guard fails the
        // save underneath them all.
        staleTrainer.Edit(staleTrainer.Name, Required(Email.Create("doomed@example.com")), staleTrainer.Bio);
        staleTrainers.Update(staleTrainer);

        var losingSave = () => scopeA.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        await losingSave.Should().ThrowAsync<ConcurrencyConflictException>();

        // One fact in the table: the winner's. The loser's row died with the loser's save.
        using var assertScope = Factory.CreateScope();
        var context = assertScope.ServiceProvider.GetRequiredService<TrainingContext>();

        var message = (await context.Set<OutboxMessage>().ToListAsync())
            .Should().ContainSingle(m => m.Name == "TrainerContactEmailChanged").Subject;

        var fact = IntegrationEventSerializer.Deserialize(message.Name, message.Version, message.Payload)
            .Should().BeOfType<TrainerContactEmailChangedIntegrationEvent>().Subject;
        fact.NewContactEmail.Should().Be("committed@example.com");
    }

    private async Task<TrainerId> SeedTrainerAsync()
    {
        using var scope = Factory.CreateScope();
        var trainers = scope.ServiceProvider.GetRequiredService<ITrainerRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var trainerId = TrainerId.Generate();
        trainers.Add(Trainer.Create(
            trainerId,
            UserId.Generate(),
            Required(Name.Create("Ada", "Lovelace")),
            Required(Email.Create("ada.lovelace@example.com")),
            bio: null));

        await unitOfWork.SaveChangesAsync();
        return trainerId;
    }

    /// <summary>
    /// Polls the outbox until the single message with <paramref name="name"/> satisfies
    /// <paramref name="condition"/>, and answers it. Fails with the message's actual state when
    /// the worker does not get there in time.
    /// </summary>
    /// <remarks>
    /// Polling is the honest shape here: the worker is a real background loop on a real timer, and
    /// the alternative — hooking its internals to signal the test — would prove a different
    /// pipeline than the one production runs. Each probe uses a fresh scope so the answer comes
    /// from the database, never from a change tracker.
    /// </remarks>
    private async Task<OutboxMessage> WaitForMessageAsync(
        string name,
        Func<OutboxMessage, bool> condition)
    {
        var timeout = TimeSpan.FromSeconds(15);
        var started = DateTime.UtcNow;
        OutboxMessage? lastSeen = null;

        while (DateTime.UtcNow - started < timeout)
        {
            using (var scope = Factory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TrainingContext>();
                lastSeen = (await context.Set<OutboxMessage>().ToListAsync())
                    .SingleOrDefault(message => message.Name == name);

                if (lastSeen is not null && condition(lastSeen))
                {
                    return lastSeen;
                }
            }

            await Task.Delay(100);
        }

        throw new InvalidOperationException(
            $"The worker did not bring '{name}' to the expected state within {timeout.TotalSeconds}s. " +
            (lastSeen is null
                ? "No such message exists."
                : $"Last seen: ProcessedOnUtc={lastSeen.ProcessedOnUtc?.ToString("O") ?? "null"}, " +
                  $"Attempts={lastSeen.Attempts}, Error={lastSeen.Error ?? "null"}."));
    }

    /// <summary>
    /// Polls the outbox until no message with <paramref name="name"/> remains — the sweep's
    /// mirror of <see cref="WaitForMessageAsync"/>. Fails with the survivor's state when the
    /// worker does not sweep it in time.
    /// </summary>
    private async Task WaitUntilGoneAsync(string name)
    {
        var timeout = TimeSpan.FromSeconds(15);
        var started = DateTime.UtcNow;
        OutboxMessage? lastSeen = null;

        while (DateTime.UtcNow - started < timeout)
        {
            using (var scope = Factory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TrainingContext>();
                lastSeen = (await context.Set<OutboxMessage>().ToListAsync())
                    .SingleOrDefault(message => message.Name == name);

                if (lastSeen is null)
                {
                    return;
                }
            }

            await Task.Delay(100);
        }

        throw new InvalidOperationException(
            $"The worker did not sweep '{name}' within {timeout.TotalSeconds}s. Last seen: " +
            $"ProcessedOnUtc={lastSeen!.ProcessedOnUtc?.ToString("O") ?? "null"}, Attempts={lastSeen.Attempts}.");
    }

    /// <summary>
    /// Unwraps a result the fixture expects to succeed. A failure here is a broken test, not a
    /// failing assertion, so it throws rather than reporting.
    /// </summary>
    private static T Required<T>(Result<T> result) => result.Match(
        value => value,
        errors => throw new InvalidOperationException(
            $"The fixture built an invalid value: {string.Join("; ", errors)}"));
}
