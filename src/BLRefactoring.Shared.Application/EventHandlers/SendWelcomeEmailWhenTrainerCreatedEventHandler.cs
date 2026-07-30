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
public sealed class SendWelcomeEmailWhenTrainerCreatedEventHandler(IEmailSender emailSender)
    : IDomainEventHandler<TrainerCreatedDomainEvent>
{
    public async ValueTask Handle(TrainerCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        var message = new EmailMessage(
            notification.Email,
            "Welcome aboard!",
            $"Hello {notification.Firstname} {notification.Lastname}, " +
            "your trainer account has been created. You can now publish your first training.");

        await emailSender.SendAsync(message, cancellationToken);
    }
}
