using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;

namespace TrainingHub.Shared.Application.EventHandlers;

/// <summary>
/// Announces a training's creation outside the bounded context: translates the domain event into
/// <see cref="TrainingCreatedIntegrationEvent"/> and stages it in the outbox.
/// </summary>
/// <remarks>
/// It replaces the handler that wrote to the search index from inside the transaction — an index
/// that could end up holding a training the database never accepted. The committed fact is what the
/// index will be rebuilt from once the delivery worker exists, and it is the same fact the
/// announced Catalogue Discovery context will subscribe to: one vocabulary, however many consumers.
/// </remarks>
public sealed class PublishIntegrationEventWhenTrainingCreatedEventHandler(IIntegrationEventPublisher publisher)
    : IDomainEventHandler<TrainingCreatedDomainEvent>
{
    /// <summary>
    /// Runs the reaction.
    /// </summary>
    /// <param name="notification">The event that was raised.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async ValueTask Handle(TrainingCreatedDomainEvent notification, CancellationToken cancellationToken) =>
        await publisher.PublishAsync(
            new TrainingCreatedIntegrationEvent(
                notification.TrainingId.Value,
                notification.TrainerId.Value),
            cancellationToken);
}
