using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;

namespace TrainingHub.Shared.Application.EventHandlers;

/// <summary>
/// Announces a sanction outside the bounded context: translates the domain event into
/// <see cref="TrainerSuspendedIntegrationEvent"/> and stages it in the outbox.
/// </summary>
/// <remarks>
/// The excuse ADR 0050 recorded — no surface raises the sanction, no context consumes it — was
/// retired by the administrative endpoints and by the two consumers waiting on this fact. Staging
/// it rather than reacting in place is what stops a trainer from being told they are suspended by
/// a transaction that then rolls back (ADR 0002).
/// </remarks>
public sealed class PublishIntegrationEventWhenTrainerSuspendedEventHandler(IIntegrationEventPublisher publisher)
    : IDomainEventHandler<TrainerSuspendedDomainEvent>
{
    /// <summary>
    /// Runs the reaction.
    /// </summary>
    /// <param name="notification">The event that was raised.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async ValueTask Handle(TrainerSuspendedDomainEvent notification, CancellationToken cancellationToken) =>
        await publisher.PublishAsync(
            new TrainerSuspendedIntegrationEvent(
                notification.TrainerId.Value,
                notification.Reason.Value),
            cancellationToken);
}
