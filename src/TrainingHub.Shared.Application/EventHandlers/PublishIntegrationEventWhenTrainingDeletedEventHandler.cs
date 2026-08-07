using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;

namespace TrainingHub.Shared.Application.EventHandlers;

/// <summary>
/// Announces a training's deletion outside the bounded context: translates the domain event into
/// <see cref="TrainingDeletedIntegrationEvent"/> and stages it in the outbox.
/// </summary>
/// <remarks>
/// Staged in the same unit of work as the deletion itself, which is the whole point of the outbox
/// here: the row and the fact that it went commit together, so no reader can be told a training
/// disappeared that is still there, nor be left serving one that is not.
/// </remarks>
public sealed class PublishIntegrationEventWhenTrainingDeletedEventHandler(IIntegrationEventPublisher publisher)
    : IDomainEventHandler<TrainingDeletedDomainEvent>
{
    /// <summary>
    /// Runs the reaction.
    /// </summary>
    /// <param name="notification">The event that was raised.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async ValueTask Handle(TrainingDeletedDomainEvent notification, CancellationToken cancellationToken) =>
        await publisher.PublishAsync(
            new TrainingDeletedIntegrationEvent(
                notification.TrainingId.Value,
                notification.TrainerId.Value),
            cancellationToken);
}
