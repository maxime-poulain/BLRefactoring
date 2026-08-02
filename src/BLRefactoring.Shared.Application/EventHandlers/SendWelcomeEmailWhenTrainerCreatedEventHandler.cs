using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;

namespace BLRefactoring.Shared.Application.EventHandlers;

/// <summary>
/// Sends a welcome email to a freshly created trainer.
/// </summary>
/// <remarks>
/// Illustrates the most common use of a domain event: reacting to a business fact
/// with a side effect the aggregate must not know about. The trainer aggregate
/// states "a trainer was created"; whether that means an email, a push
/// notification or nothing at all is an application-level decision, kept out of
/// the domain model. The handler works exclusively from the facts carried by the
/// event — it never loads the aggregate, which is not persisted yet when the
/// event is dispatched.
/// </remarks>
/// <summary>
/// Reacts to the event: welcomes the new trainer.
/// <para>
/// Dispatched inside the unit of work, before the transaction commits, so anything this handler
/// writes joins the same transaction as the change that raised the event.
/// </para>
/// </summary>
public sealed class SendWelcomeEmailWhenTrainerCreatedEventHandler(IEmailSender emailSender)
    : IDomainEventHandler<TrainerCreatedDomainEvent>
{
    /// <summary>
    /// Runs the reaction.
    /// </summary>
    /// <param name="notification">The event that was raised.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async ValueTask Handle(TrainerCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        var message = new EmailMessage(
            notification.ContactEmail.FullAddress,
            "Welcome aboard!",
            $"Hello {notification.Name.Firstname} {notification.Name.Lastname}, " +
            "your trainer account has been created. You can now publish your first training.");

        await emailSender.SendAsync(message, cancellationToken);
    }
}
