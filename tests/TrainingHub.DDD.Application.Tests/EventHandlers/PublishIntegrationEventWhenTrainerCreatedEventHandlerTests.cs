using TrainingHub.Shared.Application.EventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.EventHandlers;

/// <summary>
/// Behavior covered for <c>PublishIntegrationEventWhenTrainerCreatedEventHandler</c>.
/// </summary>
/// <remarks>
/// What is worth asserting here is the translation: value objects in, primitives out, nothing
/// dropped. Record equality makes the whole payload one assertion — a property added to the
/// integration event without a mapping shows up as an inequality, not as a silently null column.
/// </remarks>
public sealed class PublishIntegrationEventWhenTrainerCreatedEventHandlerTests
{
    private readonly Mock<IIntegrationEventPublisher> _publisher = new();

    /// <summary>
    /// Handle, publishes the trainer-created fact, flattened to primitives.
    /// </summary>
    [Fact]
    public async Task Handle_PublishesTheTrainerCreatedFact_FlattenedToPrimitives()
    {
        var trainerId = TrainerId.Generate();
        var domainEvent = new TrainerCreatedDomainEvent(
            trainerId,
            Name.Create("John", "Doe").ShouldBeSuccess(),
            Email.Create("john.doe@example.com").ShouldBeSuccess());

        var sut = new PublishIntegrationEventWhenTrainerCreatedEventHandler(_publisher.Object);
        await sut.Handle(domainEvent, CancellationToken.None);

        _publisher.Verify(p => p.PublishAsync(
                new TrainerCreatedIntegrationEvent(trainerId.Value, "John", "Doe", "john.doe@example.com"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
