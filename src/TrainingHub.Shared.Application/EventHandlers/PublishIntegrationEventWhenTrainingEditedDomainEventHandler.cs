using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;

namespace TrainingHub.Shared.Application.EventHandlers;

/// <summary>
/// Announces a training's edit outside the bounded context: translates the domain event into
/// <see cref="TrainingEditedIntegrationEvent"/> and stages it in the outbox.
/// </summary>
/// <remarks>
/// Counterpart of <see cref="PublishIntegrationEventWhenTrainingCreatedDomainEventHandler"/> for edits.
/// The two facts stay distinct on the wire for the same reason they are distinct domain events: the
/// index refresh treats them alike today, but a consumer that cares about the difference must not
/// have to guess it back out of a merged message.
/// </remarks>
public sealed class PublishIntegrationEventWhenTrainingEditedDomainEventHandler(IIntegrationEventPublisher publisher)
    : IDomainEventHandler<TrainingEditedDomainEvent>
{
    /// <summary>
    /// Runs the reaction.
    /// </summary>
    /// <param name="notification">The event that was raised.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async ValueTask Handle(TrainingEditedDomainEvent notification, CancellationToken cancellationToken) =>
        await publisher.PublishAsync(
            new TrainingEditedIntegrationEvent(
                notification.TrainingId.Value,
                notification.TrainerId.Value),
            cancellationToken);
}
