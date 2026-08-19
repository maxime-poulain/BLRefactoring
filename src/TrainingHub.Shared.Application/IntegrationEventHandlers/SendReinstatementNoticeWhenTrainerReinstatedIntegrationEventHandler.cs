using Microsoft.Extensions.Logging;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Notifications;
using TrainingHub.Shared.Application.Queries;

namespace TrainingHub.Shared.Application.IntegrationEventHandlers;

/// <summary>
/// Tells a trainer their suspension has been lifted.
/// </summary>
/// <remarks>
/// The counterpart of the suspension notice, and not an optional courtesy: a trainer who was told
/// they were sanctioned and never told it was over has no way of finding out except by trying
/// something and seeing it work. It carries no reason, because a lifting decides nothing — it
/// restores what the sanction never took away (ADR 0050).
/// </remarks>
public sealed class SendReinstatementNoticeWhenTrainerReinstatedIntegrationEventHandler(
    ITrainerAccountQuery accountQuery,
    INotificationComposer composer,
    IEmailSender emailSender,
    ILogger<SendReinstatementNoticeWhenTrainerReinstatedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<TrainerReinstatedIntegrationEvent>
{
    /// <inheritdoc />
    public string ConsumerName => "SendReinstatementNotice";

    /// <summary>
    /// Runs the reaction to a delivered fact.
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task HandleAsync(TrainerReinstatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var account = await accountQuery.GetByTrainerIdAsync(integrationEvent.TrainerId, cancellationToken);

        if (account is null)
        {
            logger.LogWarning(
                "Trainer {TrainerId} was reinstated but has no account to notify.",
                integrationEvent.TrainerId);

            return;
        }

        var notification = composer.Reinstatement(
            account.Language,
            $"{account.Firstname} {account.Lastname}");

        var message = new EmailMessage(
            account.EmailAddress,
            notification.Subject,
            notification.Body);

        await emailSender.SendAsync(message, cancellationToken);
    }
}
