using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;

namespace TrainingHub.Shared.Application.EventHandlers;

/// <summary>
/// Announces a trainer's deletion outside the bounded context: translates the domain event into
/// <see cref="TrainerDeletedIntegrationEvent"/> and stages it in the outbox.
/// </summary>
/// <remarks>
/// The last domain event to gain a publisher, and the one whose fact must carry everything it
/// will ever say: by the time the consumer runs, the trainer's rows are gone and nothing can be
/// asked. The portrait's identity rides the domain event for exactly that reason (ADR 0085), so
/// this policy flattens and asks nobody — a cross-store question here, inside the erasing save,
/// would be a second open connection in the one transaction ADR 0040 keeps local.
/// </remarks>
public sealed class PublishIntegrationEventWhenTrainerDeletedDomainEventHandler(IIntegrationEventPublisher publisher)
    : IDomainEventHandler<TrainerDeletedDomainEvent>
{
    /// <summary>
    /// Runs the reaction.
    /// </summary>
    /// <param name="notification">The event that was raised.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async ValueTask Handle(TrainerDeletedDomainEvent notification, CancellationToken cancellationToken) =>
        await publisher.PublishAsync(
            new TrainerDeletedIntegrationEvent(
                notification.TrainerId.Value,
                notification.PhotoId?.Value),
            cancellationToken);
}
