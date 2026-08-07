using AwesomeAssertions;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Infrastructure.Outbox;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Outbox;

/// <summary>
/// The serializer, held to its round trip for every registered event.
/// </summary>
/// <remarks>
/// This is the write side proving the read side in advance: the delivery worker does not exist
/// yet, and these are the tests that keep "the worker will be able to read what was stored" a fact
/// rather than a hope. Record equality compares every property, so a member that stops
/// round-tripping — renamed, computed, ignored — fails by name.
/// </remarks>
public sealed class IntegrationEventSerializerTests
{
    // The single source both the theory and its completeness guard read: one hand-built instance
    // of each registered integration event.
    private static readonly IIntegrationEvent[] Instances =
    [
        new TrainerCreatedIntegrationEvent(Guid.NewGuid(), "John", "Doe", "john.doe@example.com"),
        new TrainerContactEmailChangedIntegrationEvent(Guid.NewGuid(), "old@example.com", "new@example.com"),
        new TrainingCreatedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid()),
        new TrainingEditedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid()),
        new TrainingTransferredIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
        new TrainingPublishedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid()),
        new TrainingUnpublishedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid()),
        new TrainingDeletedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid()),
    ];

    /// <summary>One instance of each registered integration event, as theory rows.</summary>
    // Built through the constructor rather than a collection expression: the spread's lowering
    // allocates an empty array on some SDKs, which CA1825 turns into a failed build.
    public static TheoryData<IIntegrationEvent> RegisteredEvents() => new(Instances);

    /// <summary>
    /// Every registered event, survives the round trip whole.
    /// </summary>
    /// <param name="integrationEvent">The event instance to push through the round trip.</param>
    [Theory]
    [MemberData(nameof(RegisteredEvents))]
    public void EveryRegisteredEvent_SurvivesTheRoundTripWhole(IIntegrationEvent integrationEvent)
    {
        var (name, version) = IntegrationEventTypes.NameOf(integrationEvent.GetType());

        var payload = IntegrationEventSerializer.Serialize(integrationEvent);
        var roundTripped = IntegrationEventSerializer.Deserialize(name, version, payload);

        roundTripped.Should().Be(integrationEvent);
    }

    /// <summary>
    /// A payload, is never empty braces.
    /// </summary>
    /// <remarks>
    /// The one bug this serializer is built around: serializing by the declared interface type
    /// writes the interface's properties — none — and every payload becomes <c>{}</c>. The round
    /// trip alone would not catch it if deserialization defaulted the members back.
    /// </remarks>
    [Theory]
    [MemberData(nameof(RegisteredEvents))]
    public void APayload_IsNeverEmptyBraces(IIntegrationEvent integrationEvent) =>
        IntegrationEventSerializer.Serialize(integrationEvent).Should().NotBe("{}");

    /// <summary>
    /// The theory above, covers every registered event.
    /// </summary>
    /// <remarks>
    /// The vacuity guard for the round trip: without it, a fifth integration event would be
    /// registered, stored in production, and never once proven readable — the theory would stay
    /// green by testing the four it already knows.
    /// </remarks>
    [Fact]
    public void TheTheoryAbove_CoversEveryRegisteredEvent()
    {
        var covered = Instances
            .Select(instance => instance.GetType())
            .ToHashSet();

        covered.Should().BeEquivalentTo(
            IntegrationEventTypes.All,
            "every event the registry names must appear once in the round-trip theory's data");
    }
}
