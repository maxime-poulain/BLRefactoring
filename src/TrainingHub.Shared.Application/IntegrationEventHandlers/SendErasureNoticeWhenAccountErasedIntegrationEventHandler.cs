using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Notifications;

namespace TrainingHub.Shared.Application.IntegrationEventHandlers;

/// <summary>
/// Tells an erased account it was erased — the farewell, and the alarm bell.
/// </summary>
/// <remarks>
/// An owner who erased their own account reads a confirmation; an owner who did not learns
/// within seconds that somebody holding their session destroyed it (ADR 0085). There is no way
/// back to offer — the rows are gone, which is what erasure means — so the message says the one
/// thing still true: registering again starts from nothing, and nobody else can do it under the
/// erased name, because the username and address were freed with the account. Delivered
/// at-least-once; a duplicate farewell is an email, not a defect (ADR 0034).
/// </remarks>
public sealed class SendErasureNoticeWhenAccountErasedIntegrationEventHandler(IEmailSender emailSender)
    : IIntegrationEventHandler<AccountErasedIntegrationEvent>
{
    /// <inheritdoc />
    public string ConsumerName => "SendErasureNotice";

    /// <summary>
    /// Runs the reaction to a delivered fact.
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task HandleAsync(
        AccountErasedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var message = new EmailMessage(
            integrationEvent.Email,
            "Your TrainingHub account was erased",
            $"Hello {integrationEvent.Username},\n\n"
            + "Your TrainingHub account was just erased, along with your profile, your trainings "
            + "and your portrait. Nothing of it remains on the platform.\n\n"
            + "If that was you: thank you for having taught with us, and goodbye.\n\n"
            + "If it was not, somebody with access to your session erased the account. The data "
            + "cannot be restored, but the name and address are yours again: registering anew is "
            + "open to you, and closed to whoever did this the moment you do.");

        await emailSender.SendAsync(message, cancellationToken);
    }
}
