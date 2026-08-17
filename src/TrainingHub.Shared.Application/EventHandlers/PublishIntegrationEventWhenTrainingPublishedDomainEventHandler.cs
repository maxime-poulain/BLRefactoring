using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;

namespace TrainingHub.Shared.Application.EventHandlers;

/// <summary>
/// Announces a training's return to public view outside the bounded context: translates the domain
/// event into <see cref="TrainingPublishedIntegrationEvent"/> and stages it in the outbox.
/// </summary>
public sealed class PublishIntegrationEventWhenTrainingPublishedDomainEventHandler(IIntegrationEventPublisher publisher)
    : IDomainEventHandler<TrainingPublishedDomainEvent>
{
    /// <summary>
    /// Runs the reaction.
    /// </summary>
    /// <param name="notification">The event that was raised.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async ValueTask Handle(TrainingPublishedDomainEvent notification, CancellationToken cancellationToken) =>
        await publisher.PublishAsync(
            new TrainingPublishedIntegrationEvent(
                notification.TrainingId.Value,
                notification.TrainerId.Value),
            cancellationToken);
}
