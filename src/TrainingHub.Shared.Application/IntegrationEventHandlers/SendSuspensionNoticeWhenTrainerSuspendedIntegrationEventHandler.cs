using Microsoft.Extensions.Logging;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Notifications;
using TrainingHub.Shared.Application.Queries;

namespace TrainingHub.Shared.Application.IntegrationEventHandlers;

/// <summary>
/// Tells a trainer they have been suspended, and why.
/// </summary>
/// <remarks>
/// The half of ADR 0053 that does not need a screen. A sanction the product never explains is one
/// the trainer discovers by failing to do something, so the notice carries the reason the
/// administrator wrote and names the only recourse there is — an address to write to. It claims no
/// appeal flow, because there is none.
/// <para>
/// It goes to the account's address rather than to the published contact address: the contact
/// address may point at a secretariat, and this is addressed to the person who signs in
/// (ADR 0056).
/// </para>
/// </remarks>
public sealed class SendSuspensionNoticeWhenTrainerSuspendedIntegrationEventHandler(
    ITrainerAccountQuery accountQuery,
    INotificationComposer composer,
    IEmailSender emailSender,
    ILogger<SendSuspensionNoticeWhenTrainerSuspendedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<TrainerSuspendedIntegrationEvent>
{
    /// <inheritdoc />
    public string ConsumerName => "SendSuspensionNotice";

    /// <summary>
    /// Runs the reaction to a delivered fact.
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task HandleAsync(TrainerSuspendedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var account = await accountQuery.GetByTrainerIdAsync(integrationEvent.TrainerId, cancellationToken);

        if (account is null)
        {
            // Nothing to retry against: a trainer with no account left is not a transient failure,
            // and throwing would spend this consumer's whole budget re-reading the same absence
            // (ADR 0033).
            logger.LogWarning(
                "Trainer {TrainerId} was suspended but has no account to notify.",
                integrationEvent.TrainerId);

            return;
        }

        var notification = composer.Suspension(
            account.Language,
            $"{account.Firstname} {account.Lastname}",
            integrationEvent.Reason);

        var message = new EmailMessage(
            account.EmailAddress,
            notification.Subject,
            notification.Body);

        await emailSender.SendAsync(message, cancellationToken);
    }
}
