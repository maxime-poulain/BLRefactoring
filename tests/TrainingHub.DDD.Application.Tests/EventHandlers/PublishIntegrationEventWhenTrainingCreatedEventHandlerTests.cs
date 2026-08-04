using TrainingHub.Shared.Application.EventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;
using Moq;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.EventHandlers;

/// <summary>
/// Behaviour covered for <c>PublishIntegrationEventWhenTrainingCreatedEventHandler</c>.
/// </summary>
public sealed class PublishIntegrationEventWhenTrainingCreatedEventHandlerTests
{
    private readonly Mock<IIntegrationEventPublisher> _publisher = new();

    /// <summary>
    /// Handle, publishes the training-created fact, with both identifiers unwrapped.
    /// </summary>
    [Fact]
    public async Task Handle_PublishesTheTrainingCreatedFact_WithBothIdentifiersUnwrapped()
    {
        var trainingId = TrainingId.Generate();
        var trainerId = TrainerId.Generate();

        var sut = new PublishIntegrationEventWhenTrainingCreatedEventHandler(_publisher.Object);
        await sut.Handle(new TrainingCreatedDomainEvent(trainingId, trainerId), CancellationToken.None);

        _publisher.Verify(p => p.PublishAsync(
                new TrainingCreatedIntegrationEvent(trainingId.Value, trainerId.Value),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
