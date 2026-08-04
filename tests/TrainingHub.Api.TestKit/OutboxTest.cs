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
    where TFactory : IResettableDatabase, IServiceScopeSource, IHttpClientSource
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

        var message = (await context.Set<OutboxMessage>().ToListAsync())
            .Should().ContainSingle(m => m.Name == "TrainerCreated").Subject;

        message.Version.Should().Be(1);
        message.ProcessedOnUtc.Should().BeNull("nothing delivers messages yet — that is the worker's job");
        message.Attempts.Should().Be(0);

        var fact = IntegrationEventSerializer.Deserialize(message.Name, message.Version, message.Payload)
            .Should().BeOfType<TrainerCreatedIntegrationEvent>().Subject;
        fact.ContactEmail.Should().Be(request.Email);
        fact.Firstname.Should().Be(request.Firstname);
        fact.Lastname.Should().Be(request.Lastname);
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
    /// Unwraps a result the fixture expects to succeed. A failure here is a broken test, not a
    /// failing assertion, so it throws rather than reporting.
    /// </summary>
    private static T Required<T>(Result<T> result) => result.Match(
        value => value,
        errors => throw new InvalidOperationException(
            $"The fixture built an invalid value: {string.Join("; ", errors)}"));
}
