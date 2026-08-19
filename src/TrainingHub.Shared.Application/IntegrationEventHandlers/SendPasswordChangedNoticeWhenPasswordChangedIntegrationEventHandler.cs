using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Notifications;

namespace TrainingHub.Shared.Application.IntegrationEventHandlers;

/// <summary>
/// Tells an account its password was changed — the owner's alarm bell.
/// </summary>
/// <remarks>
/// The one notice whose real audience is the person it was <em>not</em> sent by: an owner who
/// changed their own password reads it and moves on, while an owner who did not now knows within
/// seconds, and the way back — a fresh reset link into the same mailbox — invalidates whatever
/// the intruder used to get in (ADR 0084). The house precedent is the warning sent to the
/// previous address when a contact email changes: a change to something that identifies you is
/// announced to who you were.
/// </remarks>
public sealed class SendPasswordChangedNoticeWhenPasswordChangedIntegrationEventHandler(
    INotificationComposer composer,
    IEmailSender emailSender)
    : IIntegrationEventHandler<PasswordChangedIntegrationEvent>
{
    /// <inheritdoc />
    public string ConsumerName => "SendPasswordChangedNotice";

    /// <summary>
    /// Runs the reaction to a delivered fact.
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task HandleAsync(
        PasswordChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var notification = composer.PasswordChanged(integrationEvent.Language, integrationEvent.Username);

        var message = new EmailMessage(
            integrationEvent.Email,
            notification.Subject,
            notification.Body);

        await emailSender.SendAsync(message, cancellationToken);
    }
}
