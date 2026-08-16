using TrainingHub.Shared.Application.Accounts;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Notifications;

namespace TrainingHub.Shared.Application.IntegrationEventHandlers;

/// <summary>
/// Mints the reset credential and mails its link — or, for an address no account listens at,
/// does nothing and says nothing.
/// </summary>
/// <remarks>
/// The whole recovery secret is born here, at delivery, and leaves only inside the email: the
/// store answers a finished link so the raw token never even crosses this consumer on its own,
/// and the outbox row that triggered the mint carries no more than the address the visitor typed
/// (ADR 0084). The silence on an unknown address is deliberate twice over — the visitor already
/// has their answer, and a log line naming a probed address would keep the probe for a year
/// (ADR 0026) — which is why this consumer holds no logger at all.
/// <para>
/// A retry after a failed send re-runs the whole issue and mails a fresh link, which the
/// latest-only invariant absorbs: whichever email arrives last is the one that works.
/// </para>
/// </remarks>
public sealed class SendPasswordResetLinkWhenPasswordResetRequestedIntegrationEventHandler(
    IPasswordResetTokenStore resetTokens,
    IEmailSender emailSender)
    : IIntegrationEventHandler<PasswordResetRequestedIntegrationEvent>
{
    /// <inheritdoc />
    public string ConsumerName => "SendPasswordResetLink";

    /// <summary>
    /// Runs the reaction to a delivered fact.
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task HandleAsync(
        PasswordResetRequestedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var invitation = await resetTokens.IssueAsync(integrationEvent.Email, cancellationToken);

        if (invitation is null)
        {
            return;
        }

        var message = new EmailMessage(
            integrationEvent.Email,
            "Reset your TrainingHub password",
            $"Hello {invitation.Username},\n\n"
            + "Somebody asked to reset the password of the TrainingHub account behind this "
            + "address — hopefully you. Choose a new password here:\n\n"
            + invitation.Link
            + "\n\n"
            + $"The link works for {invitation.Lifetime.TotalMinutes:0} minutes and only once, "
            + "and asking again replaces it.\n\n"
            + "If you did not ask for this, ignore this message — your password is unchanged "
            + "and the link above is the only way anyone could change it.");

        await emailSender.SendAsync(message, cancellationToken);
    }
}
