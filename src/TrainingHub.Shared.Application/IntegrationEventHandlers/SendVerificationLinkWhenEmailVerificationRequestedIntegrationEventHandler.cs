using TrainingHub.Shared.Application.Accounts;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Notifications;

namespace TrainingHub.Shared.Application.IntegrationEventHandlers;

/// <summary>
/// Mints the verification credential and mails its link — or, for an account that is gone or has
/// already proven its address, does nothing and says nothing.
/// </summary>
/// <remarks>
/// The reset consumer's shape (ADR 0084) with its silences kept: the credential is born here, at
/// delivery, and leaves only inside the email, so the outbox row never carries the secret; and a
/// null mint is absorbed without a log line, because it is the ordinary end of two honest races —
/// a resend that lost to the click, and a retry re-running a delivery whose account verified in
/// the meantime (ADR 0090). This consumer holds no logger at all for ADR 0084's reason.
/// <para>
/// The words are not composed here: the invitation's language, name and link go to
/// <see cref="INotificationComposer"/>, and what comes back is prose this layer never inspects —
/// the application knows no language, the same way it knows no URL (ADR 0090). The language rides
/// on the invitation rather than on the fact since ADR 0091, beside the address it belongs to.
/// </para>
/// </remarks>
public sealed class SendVerificationLinkWhenEmailVerificationRequestedIntegrationEventHandler(
    IEmailVerificationTokenStore verificationTokens,
    INotificationComposer composer,
    IEmailSender emailSender)
    : IIntegrationEventHandler<EmailVerificationRequestedIntegrationEvent>
{
    /// <inheritdoc />
    public string ConsumerName => "SendVerificationLink";

    /// <summary>
    /// Runs the reaction to a delivered fact.
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task HandleAsync(
        EmailVerificationRequestedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var invitation = await verificationTokens.IssueAsync(integrationEvent.UserId, cancellationToken);

        if (invitation is null)
        {
            return;
        }

        var notification = composer.VerificationLink(
            invitation.Language,
            invitation.Username,
            invitation.Link);

        var message = new EmailMessage(
            invitation.EmailAddress,
            notification.Subject,
            notification.Body);

        await emailSender.SendAsync(message, cancellationToken);
    }
}
