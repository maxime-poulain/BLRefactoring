using AwesomeAssertions;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Infrastructure.Outbox;
using Moq;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Outbox;

/// <summary>
/// The dispatcher, held to its routing table.
/// </summary>
/// <remarks>
/// The dispatcher's switch restates the registry's closed set where the routing happens, and this
/// is the test that keeps the two lists from drifting: every registered event must route without
/// refusal, and anything the registry never named must be refused loudly rather than dropped.
/// </remarks>
public sealed class IntegrationEventDispatcherTests
{
    /// <summary>An event the registry and the dispatcher have never heard of.</summary>
    private sealed record UnroutedIntegrationEvent : IIntegrationEvent;

    // One hand-built instance per registered event, like the serializer's own guard.
    private static readonly IIntegrationEvent[] Instances =
    [
        new TrainerCreatedIntegrationEvent(Guid.NewGuid(), "John", "Doe", "john.doe@example.com"),
        new TrainerContactEmailChangedIntegrationEvent(Guid.NewGuid(), "old@example.com", "new@example.com"),
        new TrainingCreatedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid()),
        new TrainingEditedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid()),
    ];

    private static IntegrationEventDispatcher CreateSutWithoutConsumers() => new([], [], [], []);

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
            var act = () => sut.DispatchAsync(instance, CancellationToken.None);
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
        var first = new Mock<IIntegrationEventHandler<TrainerCreatedIntegrationEvent>>();
        var second = new Mock<IIntegrationEventHandler<TrainerCreatedIntegrationEvent>>();
        var sut = new IntegrationEventDispatcher([first.Object, second.Object], [], [], []);

        var fact = new TrainerCreatedIntegrationEvent(Guid.NewGuid(), "Ada", "Lovelace", "ada@example.com");
        await sut.DispatchAsync(fact, CancellationToken.None);

        first.Verify(h => h.HandleAsync(fact, It.IsAny<CancellationToken>()), Times.Once);
        second.Verify(h => h.HandleAsync(fact, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// An unrouted event, is refused by name.
    /// </summary>
    [Fact]
    public async Task AnUnroutedEvent_IsRefusedByName()
    {
        var sut = CreateSutWithoutConsumers();

        var act = () => sut.DispatchAsync(new UnroutedIntegrationEvent(), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage($"*{nameof(UnroutedIntegrationEvent)}*no route*");
    }
}
