using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;

namespace TrainingHub.Shared.Application.EventHandlers;

/// <summary>
/// Announces a contact-address change outside the bounded context: translates the domain event into
/// <see cref="TrainerContactEmailChangedIntegrationEvent"/> and stages it in the outbox.
/// </summary>
/// <remarks>
/// The reaction this fact exists for — warning the address being left behind, so the legitimate
/// owner can react to a change that was not theirs — is security-sensitive enough that it must
/// never fire for a change that did not commit. Staging the fact in the outbox gives exactly that:
/// the row and the new address land in one transaction, and the warning becomes the delivery
/// worker's reaction to a change that is real. Both addresses are flattened into the message
/// because the aggregate has already forgotten the old one.
/// </remarks>
public sealed class PublishIntegrationEventWhenTrainerContactEmailChangedDomainEventHandler(IIntegrationEventPublisher publisher)
    : IDomainEventHandler<TrainerContactEmailChangedDomainEvent>
{
    /// <summary>
    /// Runs the reaction.
    /// </summary>
    /// <param name="notification">The event that was raised.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async ValueTask Handle(TrainerContactEmailChangedDomainEvent notification, CancellationToken cancellationToken) =>
        await publisher.PublishAsync(
            new TrainerContactEmailChangedIntegrationEvent(
                notification.TrainerId.Value,
                notification.OldContactEmail.FullAddress,
                notification.NewContactEmail.FullAddress),
            cancellationToken);
}
