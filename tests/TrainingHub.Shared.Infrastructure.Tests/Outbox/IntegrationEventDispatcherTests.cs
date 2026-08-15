using AwesomeAssertions;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Infrastructure.Outbox;
using Moq;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Outbox;

/// <summary>
/// The dispatcher, held to its routing table and to its isolation contract.
/// </summary>
/// <remarks>
/// The dispatcher's switch restates the registry's closed set where the routing happens, and the
/// first facts keep the two lists from drifting: every registered event must route without
/// refusal, and anything the registry never named must be refused loudly rather than dropped.
/// The later facts hold ADR 0034's isolation: a consumer's failure is its own outcome, never its
/// neighbor's, and a delivery the ledger already recorded is never run again.
/// </remarks>
public sealed class IntegrationEventDispatcherTests
{
    private static readonly IReadOnlySet<string> NothingDeliveredYet = new HashSet<string>();

    /// <summary>An event the registry and the dispatcher have never heard of.</summary>
    private sealed record UnroutedIntegrationEvent : IIntegrationEvent;

    // One hand-built instance per registered event, like the serializer's own guard.
    private static readonly IIntegrationEvent[] Instances =
    [
        new TrainerCreatedIntegrationEvent(Guid.NewGuid(), "John", "Doe", "john.doe@example.com"),
        new TrainerContactEmailChangedIntegrationEvent(Guid.NewGuid(), "old@example.com", "new@example.com"),
        new TrainerSuspendedIntegrationEvent(Guid.NewGuid(), "Repeated breaches of the content policy."),
        new TrainerReinstatedIntegrationEvent(Guid.NewGuid()),
        new TrainerContactedIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), "Ada", "Lovelace", "ada@example.com", "Is this course running in June?"),
        new TrainingCreatedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid()),
        new TrainingEditedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid()),
        new TrainingTransferredIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
        new TrainingPublishedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid()),
        new TrainingUnpublishedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid()),
        new TrainingWithheldIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), "A withheld training", "Misleading claims."),
        new TrainingDeletedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid()),
    ];

    private static IntegrationEventDispatcher CreateSutWithoutConsumers() => new([], [], [], [], [], [], [], [], [], [], [], []);

    private static Mock<IIntegrationEventHandler<TrainerCreatedIntegrationEvent>> CreateConsumer(string name)
    {
        var consumer = new Mock<IIntegrationEventHandler<TrainerCreatedIntegrationEvent>>();
        consumer.SetupGet(handler => handler.ConsumerName).Returns(name);
        return consumer;
    }

    /// <summary>
    /// Every registered event, has a route.
    /// </summary>
    /// <remarks>
    /// Dispatched with no consumer attached: no consumer answering is a legitimate runtime state
    /// (the architecture rule is what demands consumers exist), but no *route* existing would be
    /// the switch lagging behind the registry, and that must fail here before it fails in the
    /// worker.
    /// </remarks>
    [Fact]
    public async Task EveryRegisteredEvent_HasARoute()
    {
        var sut = CreateSutWithoutConsumers();

        foreach (var instance in Instances)
        {
            var act = () => sut.DispatchAsync(instance, NothingDeliveredYet, CancellationToken.None);
            await act.Should().NotThrowAsync($"{instance.GetType().Name} is registered, so the switch must route it");
        }

        Instances.Select(instance => instance.GetType())
            .Should().BeEquivalentTo(IntegrationEventTypes.All,
                "every event the registry names must appear once in this test's instances");
    }

    /// <summary>
    /// Dispatch, reaches every registered consumer, with the fact intact.
    /// </summary>
    [Fact]
    public async Task Dispatch_ReachesEveryRegisteredConsumer_WithTheFactIntact()
    {
        var first = CreateConsumer("First");
        var second = CreateConsumer("Second");
        var sut = new IntegrationEventDispatcher([first.Object, second.Object], [], [], [], [], [], [], [], [], [], [], []);

        var fact = new TrainerCreatedIntegrationEvent(Guid.NewGuid(), "Ada", "Lovelace", "ada@example.com");
        var outcome = await sut.DispatchAsync(fact, NothingDeliveredYet, CancellationToken.None);

        first.Verify(h => h.HandleAsync(fact, It.IsAny<CancellationToken>()), Times.Once);
        second.Verify(h => h.HandleAsync(fact, It.IsAny<CancellationToken>()), Times.Once);
        outcome.Delivered.Should().Equal("First", "Second");
        outcome.EveryConsumerSettled.Should().BeTrue();
    }

    /// <summary>
    /// A throwing consumer, does not stop its neighbor.
    /// </summary>
    [Fact]
    public async Task AThrowingConsumer_DoesNotStopItsNeighbor()
    {
        var first = CreateConsumer("First");
        var thrown = new InvalidOperationException("the reaction failed");
        first.Setup(h => h.HandleAsync(It.IsAny<TrainerCreatedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(thrown);
        var second = CreateConsumer("Second");
        var sut = new IntegrationEventDispatcher([first.Object, second.Object], [], [], [], [], [], [], [], [], [], [], []);

        var fact = new TrainerCreatedIntegrationEvent(Guid.NewGuid(), "Ada", "Lovelace", "ada@example.com");
        var outcome = await sut.DispatchAsync(fact, NothingDeliveredYet, CancellationToken.None);

        second.Verify(h => h.HandleAsync(fact, It.IsAny<CancellationToken>()), Times.Once,
            "a neighbor's failure is the neighbor's outcome, not this consumer's");
        outcome.Delivered.Should().Equal("Second");
        outcome.Failures.Should().ContainSingle()
            .Which.Should().Be(new ConsumerFailure("First", thrown));
        outcome.EveryConsumerSettled.Should().BeFalse();
    }

    /// <summary>
    /// An already delivered consumer, is not run again.
    /// </summary>
    [Fact]
    public async Task AnAlreadyDeliveredConsumer_IsNotRunAgain()
    {
        var first = CreateConsumer("First");
        var second = CreateConsumer("Second");
        var sut = new IntegrationEventDispatcher([first.Object, second.Object], [], [], [], [], [], [], [], [], [], [], []);

        var fact = new TrainerCreatedIntegrationEvent(Guid.NewGuid(), "Ada", "Lovelace", "ada@example.com");
        var outcome = await sut.DispatchAsync(fact, new HashSet<string> { "First" }, CancellationToken.None);

        first.Verify(h => h.HandleAsync(It.IsAny<TrainerCreatedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never, "what the ledger already records must not be delivered twice");
        second.Verify(h => h.HandleAsync(fact, It.IsAny<CancellationToken>()), Times.Once);
        outcome.Delivered.Should().Equal(["Second"],
            "the skipped consumer ran in an earlier pass, and reporting it again would write a duplicate ledger row");
        outcome.EveryConsumerSettled.Should().BeTrue();
    }

    /// <summary>
    /// An unrouted event, is refused by name.
    /// </summary>
    [Fact]
    public async Task AnUnroutedEvent_IsRefusedByName()
    {
        var sut = CreateSutWithoutConsumers();

        var act = () => sut.DispatchAsync(new UnroutedIntegrationEvent(), NothingDeliveredYet, CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage($"*{nameof(UnroutedIntegrationEvent)}*no route*");
    }
}
