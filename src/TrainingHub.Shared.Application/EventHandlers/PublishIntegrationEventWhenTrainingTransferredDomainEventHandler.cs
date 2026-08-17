using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;

namespace TrainingHub.Shared.Application.EventHandlers;

/// <summary>
/// Announces a training's transfer outside the bounded context: translates the domain event into
/// <see cref="TrainingTransferredIntegrationEvent"/> and stages it in the outbox.
/// </summary>
/// <remarks>
/// The one reaction the transfer needs today is the index moving the entry to its new owner, but
/// the fact carries both owners for whoever comes next — the same reason the contact-email fact
/// carries the old address (ADR 0036).
/// </remarks>
public sealed class PublishIntegrationEventWhenTrainingTransferredDomainEventHandler(IIntegrationEventPublisher publisher)
    : IDomainEventHandler<TrainingTransferredDomainEvent>
{
    /// <summary>
    /// Runs the reaction.
    /// </summary>
    /// <param name="notification">The event that was raised.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async ValueTask Handle(TrainingTransferredDomainEvent notification, CancellationToken cancellationToken) =>
        await publisher.PublishAsync(
            new TrainingTransferredIntegrationEvent(
                notification.TrainingId.Value,
                notification.FormerTrainerId.Value,
                notification.NewTrainerId.Value),
            cancellationToken);
}
