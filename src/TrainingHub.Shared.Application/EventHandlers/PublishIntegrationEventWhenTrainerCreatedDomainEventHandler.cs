using System.Globalization;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;

namespace TrainingHub.Shared.Application.EventHandlers;

/// <summary>
/// Announces a trainer's creation outside the bounded context: translates the domain event into
/// <see cref="TrainerCreatedIntegrationEvent"/> and stages it in the outbox.
/// </summary>
/// <remarks>
/// This handler is the seam ADR 0002 draws. It replaces the handler that sent the welcome email
/// from inside the transaction — an email that could go out for a save that then failed. Now the
/// handler only records the fact: the outbox row joins the same <c>SaveChanges</c> as the trainer
/// itself, so both commit or neither does, and the welcome email becomes the delivery worker's
/// reaction to the committed fact. The translation to primitives happens here because this is the
/// last place the domain's value objects are in scope and the first place they must not leak.
/// </remarks>
public sealed class PublishIntegrationEventWhenTrainerCreatedDomainEventHandler(IIntegrationEventPublisher publisher)
    : IDomainEventHandler<TrainerCreatedDomainEvent>
{
    /// <summary>
    /// Runs the reaction.
    /// </summary>
    /// <param name="notification">The event that was raised.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async ValueTask Handle(TrainerCreatedDomainEvent notification, CancellationToken cancellationToken) =>
        await publisher.PublishAsync(
            new TrainerCreatedIntegrationEvent(
                notification.TrainerId.Value,
                notification.Name.Firstname,
                notification.Name.Lastname,
                notification.ContactEmail.FullAddress,
                // The one notice whose language is the request's rather than the store's, and the
                // only place where the two cannot differ: this runs inside the registration that
                // is writing the account's preference from this very culture, against a row that
                // has not committed yet (ADR 0091).
                CultureInfo.CurrentUICulture.Name),
            cancellationToken);
}
