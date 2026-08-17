using TrainingHub.Shared.Application.EventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;
using Moq;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.EventHandlers;

/// <summary>
/// Behavior covered for <c>PublishIntegrationEventWhenTrainingTransferredDomainEventHandler</c>.
/// </summary>
/// <remarks>
/// The verification pins both owners on the wire: a consumer reacting to the transfer must not
/// have to remember who used to hold the training (ADR 0036).
/// </remarks>
public sealed class PublishIntegrationEventWhenTrainingTransferredDomainEventHandlerTests
{
    private readonly Mock<IIntegrationEventPublisher> _publisher = new();

    /// <summary>
    /// Handle, publishes the transferred fact, carrying both owners.
    /// </summary>
    [Fact]
    public async Task Handle_PublishesTheTransferredFact_CarryingBothOwners()
    {
        var trainingId = TrainingId.Generate();
        var formerOwner = TrainerId.Generate();
        var newOwner = TrainerId.Generate();

        var sut = new PublishIntegrationEventWhenTrainingTransferredDomainEventHandler(_publisher.Object);
        await sut.Handle(
            new TrainingTransferredDomainEvent(trainingId, formerOwner, newOwner),
            CancellationToken.None);

        _publisher.Verify(p => p.PublishAsync(
                new TrainingTransferredIntegrationEvent(trainingId.Value, formerOwner.Value, newOwner.Value),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
