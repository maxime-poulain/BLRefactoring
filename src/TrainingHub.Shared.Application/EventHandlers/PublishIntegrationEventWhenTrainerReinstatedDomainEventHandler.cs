using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;

namespace TrainingHub.Shared.Application.EventHandlers;

/// <summary>
/// Announces a lifted sanction outside the bounded context: translates the domain event into
/// <see cref="TrainerReinstatedIntegrationEvent"/> and stages it in the outbox.
/// </summary>
/// <remarks>
/// The half that keeps the suspension a state rather than a tombstone, on the consuming side too:
/// without this fact the catalog would leave public view on the way in and never come back, and
/// the sanction the domain models as reversible would be irreversible everywhere a visitor looks
/// (ADR 0050, ADR 0056).
/// </remarks>
public sealed class PublishIntegrationEventWhenTrainerReinstatedDomainEventHandler(IIntegrationEventPublisher publisher)
    : IDomainEventHandler<TrainerReinstatedDomainEvent>
{
    /// <summary>
    /// Runs the reaction.
    /// </summary>
    /// <param name="notification">The event that was raised.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async ValueTask Handle(TrainerReinstatedDomainEvent notification, CancellationToken cancellationToken) =>
        await publisher.PublishAsync(
            new TrainerReinstatedIntegrationEvent(notification.TrainerId.Value),
            cancellationToken);
}
