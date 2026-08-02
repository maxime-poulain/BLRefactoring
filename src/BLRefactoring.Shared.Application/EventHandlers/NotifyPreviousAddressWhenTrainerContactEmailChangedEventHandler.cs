using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;

namespace BLRefactoring.Shared.Application.EventHandlers;

/// <summary>
/// Notifies the previous contact address when a trainer's contact email changes.
/// </summary>
/// <remarks>
/// Illustrates a security-driven use of a domain event: warning the old address
/// gives the legitimate owner a chance to react if the change was not theirs.
/// This is only possible because the event carries both the old and the new
/// address — the aggregate has already forgotten the old one by the time the
/// event is handled.
/// </remarks>
/// <summary>
/// Reacts to the event: warns the address being left behind.
/// <para>
/// Dispatched inside the unit of work, before the transaction commits, so anything this handler
/// writes joins the same transaction as the change that raised the event.
/// </para>
/// </summary>
public sealed class NotifyPreviousAddressWhenTrainerContactEmailChangedEventHandler(IEmailSender emailSender)
    : IDomainEventHandler<TrainerContactEmailChangedDomainEvent>
{
    /// <summary>
    /// Runs the reaction.
    /// </summary>
    /// <param name="notification">The event that was raised.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async ValueTask Handle(TrainerContactEmailChangedDomainEvent notification, CancellationToken cancellationToken)
    {
        var message = new EmailMessage(
            notification.OldContactEmail.FullAddress,
            "Your contact email address was changed",
            $"The contact email address of your trainer profile was changed to {notification.NewContactEmail.FullAddress}. " +
            "If you did not request this change, please contact support immediately.");

        await emailSender.SendAsync(message, cancellationToken);
    }
}
