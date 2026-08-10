using TrainingHub.Shared.Application.EventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;
using Moq;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.EventHandlers;

/// <summary>
/// Behavior covered for <c>PublishIntegrationEventWhenTrainingEditedEventHandler</c>.
/// </summary>
/// <remarks>
/// Asserts the edited fact stays an edited fact on the wire: the type in the verification is what
/// keeps a well-meaning merge of "created" and "edited" into one message from passing unnoticed.
/// </remarks>
public sealed class PublishIntegrationEventWhenTrainingEditedEventHandlerTests
{
    private readonly Mock<IIntegrationEventPublisher> _publisher = new();

    /// <summary>
    /// Handle, publishes the training-edited fact, not the created one.
    /// </summary>
    [Fact]
    public async Task Handle_PublishesTheTrainingEditedFact_NotTheCreatedOne()
    {
        var trainingId = TrainingId.Generate();
        var trainerId = TrainerId.Generate();

        var sut = new PublishIntegrationEventWhenTrainingEditedEventHandler(_publisher.Object);
        await sut.Handle(new TrainingEditedDomainEvent(trainingId, trainerId), CancellationToken.None);

        _publisher.Verify(p => p.PublishAsync(
                new TrainingEditedIntegrationEvent(trainingId.Value, trainerId.Value),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
