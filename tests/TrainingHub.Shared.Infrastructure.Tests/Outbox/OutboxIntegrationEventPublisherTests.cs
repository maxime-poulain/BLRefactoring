using AwesomeAssertions;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Infrastructure.Outbox;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Outbox;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Outbox;

/// <summary>
/// The publisher, observed through the change tracker it stages rows into.
/// </summary>
/// <remarks>
/// The context is built against the real SQL Server provider but never saved: staging is the
/// publisher's entire job — the commit belongs to whatever unit of work is in flight — so the
/// change tracker holds everything there is to assert. The relational provider matters even
/// unconnected: it validates the outbox mapping (the filtered index, the precisions) that the
/// in-memory provider would silently accept in any state.
/// </remarks>
public sealed class OutboxIntegrationEventPublisherTests
{
    /// <summary>A clock stopped at a value the assertions can name.</summary>
    private sealed class StoppedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 4, 12, 30, 0, TimeSpan.Zero);

    private static TrainingContext CreateUnconnectedContext() =>
        new(new DbContextOptionsBuilder<TrainingContext>()
            // A syntactically valid connection string nothing will ever open: building the model
            // and tracking entities are both offline operations.
            .UseSqlServer("Server=unreachable;Database=unused;Integrated Security=false;User Id=nobody;Password=nothing;TrustServerCertificate=true")
            .Options);

    /// <summary>
    /// Publish, stages one enveloped row, in the ambient unit of work.
    /// </summary>
    [Fact]
    public async Task Publish_StagesOneEnvelopedRow_InTheAmbientUnitOfWork()
    {
        await using var context = CreateUnconnectedContext();
        var sut = new OutboxIntegrationEventPublisher(context, new StoppedClock(Now));
        var integrationEvent = new TrainerCreatedIntegrationEvent(
            Guid.NewGuid(), "John", "Doe", "john.doe@example.com", "en");

        await sut.PublishAsync(integrationEvent);

        var staged = context.ChangeTracker.Entries<OutboxMessage>().Should().ContainSingle().Subject;
        staged.State.Should().Be(EntityState.Added);

        var message = staged.Entity;
        message.Id.Should().NotBeEmpty();
        message.Name.Should().Be("TrainerCreated");
        message.Version.Should().Be(1);
        message.OccurredOnUtc.Should().Be(Now.UtcDateTime);
        message.ProcessedOnUtc.Should().BeNull("delivery is the worker's job, after the commit");
        message.Attempts.Should().Be(0);
        message.Error.Should().BeNull();
    }

    /// <summary>
    /// Publish, writes a payload, the read path gives back whole.
    /// </summary>
    [Fact]
    public async Task Publish_WritesAPayload_TheReadPathGivesBackWhole()
    {
        await using var context = CreateUnconnectedContext();
        var sut = new OutboxIntegrationEventPublisher(context, new StoppedClock(Now));
        var integrationEvent = new TrainerCreatedIntegrationEvent(
            Guid.NewGuid(), "Ada", "Lovelace", "ada.lovelace@example.com", "en");

        await sut.PublishAsync(integrationEvent);

        var message = context.ChangeTracker.Entries<OutboxMessage>().Single().Entity;
        IntegrationEventSerializer.Deserialize(message.Name, message.Version, message.Payload)
            .Should().Be(integrationEvent);
    }

    /// <summary>
    /// Publish, mints a fresh identifier per message, so consumers can deduplicate.
    /// </summary>
    [Fact]
    public async Task Publish_MintsAFreshIdentifierPerMessage_SoConsumersCanDeduplicate()
    {
        await using var context = CreateUnconnectedContext();
        var sut = new OutboxIntegrationEventPublisher(context, new StoppedClock(Now));
        var integrationEvent = new TrainingCreatedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid());

        await sut.PublishAsync(integrationEvent);
        await sut.PublishAsync(integrationEvent);

        // The same fact published twice is two messages: identity belongs to the envelope, not to
        // the payload, which is what lets a consumer tell a duplicate delivery from a repeat fact.
        context.ChangeTracker.Entries<OutboxMessage>()
            .Select(entry => entry.Entity.Id)
            .Should().OnlyHaveUniqueItems();
    }
}
