using Microsoft.Extensions.Logging;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Notifications;
using TrainingHub.Shared.Application.Queries;

namespace TrainingHub.Shared.Application.IntegrationEventHandlers;

/// <summary>
/// Tells a trainer that one of their trainings has been taken out of public view, and why.
/// </summary>
/// <remarks>
/// ADR 0052 made withholding a state distinct from withdrawal precisely so that the owner could be
/// told the difference: what they withdrew, they may publish again; what was taken from them, they
/// may not. A notice that did not name the training would leave an owner with a dozen of them to
/// guess, which is why the title travels with the fact.
/// <para>
/// It states the reason and stops there. Explaining how the decision was reached would publish a
/// moderation policy to the person most motivated to work around it (ADR 0052).
/// </para>
/// </remarks>
public sealed class SendWithholdingNoticeWhenTrainingWithheldIntegrationEventHandler(
    ITrainerAccountQuery accountQuery,
    IEmailSender emailSender,
    ILogger<SendWithholdingNoticeWhenTrainingWithheldIntegrationEventHandler> logger)
    : IIntegrationEventHandler<TrainingWithheldIntegrationEvent>
{
    /// <inheritdoc />
    public string ConsumerName => "SendWithholdingNotice";

    /// <summary>
    /// Runs the reaction to a delivered fact.
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task HandleAsync(TrainingWithheldIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var account = await accountQuery.GetByTrainerIdAsync(integrationEvent.TrainerId, cancellationToken);

        if (account is null)
        {
            logger.LogWarning(
                "Training {TrainingId} was withheld but its owner {TrainerId} has no account to notify.",
                integrationEvent.TrainingId,
                integrationEvent.TrainerId);

            return;
        }

        var message = new EmailMessage(
            account.EmailAddress,
            "One of your trainings has been withheld",
            $"Hello {account.Firstname} {account.Lastname}, your training \"{integrationEvent.Title}\" "
            + $"has been withheld for the following reason: {integrationEvent.Reason}. "
            + "It is no longer offered publicly, and you cannot publish it again yourself. "
            + "If you believe this is a mistake, reply to this message.");

        await emailSender.SendAsync(message, cancellationToken);
    }
}
