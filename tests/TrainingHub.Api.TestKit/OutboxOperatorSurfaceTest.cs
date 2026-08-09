using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainingHub.Shared.Api.Contracts.Outbox;
using TrainingHub.Shared.Api.Contracts.Pagination;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Outbox;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// The outbox's operator surface over HTTP, on both hosts (ADR 0061).
/// </summary>
/// <remarks>
/// The listing and the refusals are proven where they live, against SQLite. What only this suite can
/// prove is what they add up to against the running system: that a poison row an operator requeues
/// is actually picked up again by the worker, and that nobody but an administrator can ask.
/// <para>
/// The rows are planted rather than produced, for the reason <c>OutboxTest</c> plants its own: a
/// message the registry cannot read fails at deserialization on every attempt, which is the only way
/// to get a deterministic poison row without breaking a consumer the rest of the suite depends on.
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class OutboxOperatorSurfaceTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource, IServiceScopeSource
{
    /// <summary>
    /// A poison message, is listed for an administrator, with the reason it stopped.
    /// </summary>
    [Fact]
    public async Task APoisonMessage_IsListedForAnAdministrator_WithTheReasonItStopped()
    {
        var messageId = await PlantPoisonAsync("NobodyKnowsThisName");
        await PlantSettledDeliveryAsync(messageId, "AConsumerThatAlreadySucceeded");

        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        var page = await ReadPoisonAsync(administrator);

        var row = page.Items.Should().ContainSingle(
            candidate => candidate.Id == messageId).Subject;

        row.Name.Should().Be("NobodyKnowsThisName");
        row.Attempts.Should().BeGreaterThan(0, "a poison row is one whose budget is spent");
        row.Error.Should().NotBeNullOrWhiteSpace("the evidence is why the listing exists");
        row.DeliveredConsumers.Should().Equal(["AConsumerThatAlreadySucceeded"],
            "the ledger says what a retry will not run again, which is what makes pressing it a decision");
    }

    /// <summary>
    /// Requeueing a poison message, hands it back to the worker, which delivers it.
    /// </summary>
    /// <remarks>
    /// The one fact nothing else can give. The planted message carries a wire name the registry does
    /// know and a payload it can read, so the only thing keeping it undelivered is the spent budget —
    /// which means the delivery that follows the requeue proves the row really did return to the
    /// pool, rather than merely having its columns rewritten.
    /// </remarks>
    [Fact]
    public async Task RequeueingAPoisonMessage_HandsItBackToTheWorker_WhichDeliversIt()
    {
        var messageId = await PlantPoisonAsync("TrainerSuspended", payload: SuspensionPayload());

        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        var response = await administrator.PostAsync(
            $"/Administration/Outbox/poison/{messageId}/requeue", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var delivered = await WaitForDeliveryAsync(messageId);

        delivered.ProcessedOnUtc.Should().NotBeNull(
            "a requeued message is claimable again, so the worker's next poll takes it");
        delivered.RequeuedOnUtc.Should().NotBeNull("the row remembers that somebody asked");

        var stillListed = await ReadPoisonAsync(administrator);
        stillListed.Items.Should().NotContain(candidate => candidate.Id == messageId,
            "a message that delivered is no longer poison, so it leaves the listing it was read from");
    }

    /// <summary>
    /// Requeueing a message nobody stored, answers 404.
    /// </summary>
    [Fact]
    public async Task RequeueingAMessageNobodyStored_Answers404()
    {
        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        var response = await administrator.PostAsync(
            $"/Administration/Outbox/poison/{Guid.CreateVersion7()}/requeue", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Requeueing a message that is not poison, answers 409.
    /// </summary>
    /// <remarks>
    /// Delivered is the state that matters: accepting it would replay consumers the ledger has
    /// already settled, which is exactly what the ledger exists to prevent (ADR 0034).
    /// </remarks>
    [Fact]
    public async Task RequeueingAMessageThatIsNotPoison_Answers409()
    {
        var messageId = await PlantDeliveredAsync("AlreadyDelivered");

        var administrator = await AuthHelper.SignInAsAdministratorAsync(Factory);

        var response = await administrator.PostAsync(
            $"/Administration/Outbox/poison/{messageId}/requeue", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// The operator surface, is refused to a trainer.
    /// </summary>
    /// <remarks>
    /// The listing publishes exception text and the requeue moves work through the system; both are
    /// an administrator's, and the base class is what says so — asserted here rather than trusted.
    /// </remarks>
    [Fact]
    public async Task TheOperatorSurface_IsRefusedToATrainer()
    {
        var trainer = await AuthHelper.RegisterAndGetAuthenticatedClientAsync(Factory);

        (await trainer.GetAsync("/Administration/Outbox/poison"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await trainer.PostAsync($"/Administration/Outbox/poison/{Guid.CreateVersion7()}/requeue", content: null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static string SuspensionPayload() =>
        $$"""
          {"TrainerId":"{{Guid.CreateVersion7()}}","ContactEmail":"requeued@example.com","Reason":"Requeued by an operator."}
          """;

    private static async Task<PagedHttpResponse<PoisonedMessageHttpResponse>> ReadPoisonAsync(HttpClient client)
    {
        var response = await client.GetAsync("/Administration/Outbox/poison");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await response.Content.ReadFromJsonAsync<PagedHttpResponse<PoisonedMessageHttpResponse>>();

        return page!;
    }

    /// <summary>
    /// Plants a row whose retry budget is already spent, so it is poison the moment it exists.
    /// </summary>
    private async Task<Guid> PlantPoisonAsync(string name, string payload = "{}")
    {
        var messageId = Guid.CreateVersion7();

        using var scope = Factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrainingContext>();

        var message = new OutboxMessage(messageId, name, 1, payload, DateTime.UtcNow);

        // Spent against the suite's own budget rather than a number written here: the fixture
        // shrinks MaxAttempts, and a hard-coded count would plant a row the worker still owns.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            message.RecordFailure("planted", DateTime.UtcNow, TimeSpan.FromSeconds(30));
        }

        context.Set<OutboxMessage>().Add(message);
        await context.SaveChangesAsync();

        return messageId;
    }

    private async Task<Guid> PlantDeliveredAsync(string name)
    {
        var messageId = Guid.CreateVersion7();

        using var scope = Factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrainingContext>();

        var message = new OutboxMessage(messageId, name, 1, "{}", DateTime.UtcNow);
        message.MarkProcessed(DateTime.UtcNow);

        context.Set<OutboxMessage>().Add(message);
        await context.SaveChangesAsync();

        return messageId;
    }

    private async Task PlantSettledDeliveryAsync(Guid messageId, string consumerName)
    {
        using var scope = Factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrainingContext>();

        context.Set<OutboxMessageConsumer>().Add(
            new OutboxMessageConsumer(messageId, consumerName, DateTime.UtcNow));

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Polls until the worker has delivered the requeued message, or says what it was still doing.
    /// </summary>
    private async Task<OutboxMessage> WaitForDeliveryAsync(Guid messageId)
    {
        var timeout = TimeSpan.FromSeconds(15);
        var started = DateTime.UtcNow;
        OutboxMessage? lastSeen = null;

        while (DateTime.UtcNow - started < timeout)
        {
            using (var scope = Factory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TrainingContext>();
                lastSeen = await context.Set<OutboxMessage>()
                    .SingleOrDefaultAsync(message => message.Id == messageId);

                if (lastSeen?.ProcessedOnUtc is not null)
                {
                    return lastSeen;
                }
            }

            await Task.Delay(100);
        }

        throw new InvalidOperationException(
            $"The worker did not deliver the requeued message within {timeout.TotalSeconds}s. " +
            (lastSeen is null
                ? "The row is gone."
                : $"Last seen: Attempts={lastSeen.Attempts}, Error={lastSeen.Error ?? "null"}."));
    }
}
