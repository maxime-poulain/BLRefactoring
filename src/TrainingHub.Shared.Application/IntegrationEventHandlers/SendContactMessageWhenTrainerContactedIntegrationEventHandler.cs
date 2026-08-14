using Microsoft.Extensions.Logging;
using TrainingHub.Shared.Application.Catalog;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Notifications;
using TrainingHub.Shared.Application.Queries;

namespace TrainingHub.Shared.Application.IntegrationEventHandlers;

/// <summary>
/// Carries a visitor's message to the trainer they wrote to.
/// </summary>
/// <remarks>
/// The only consumer here that forwards somebody else's words, and the only place in this
/// application that reads <c>Trainer.ContactEmail</c> to send anything. It is the reason
/// <see cref="ITrainerContactQuery"/> exists: the address is resolved here, at delivery, so it is
/// never written to the outbox, never bound to a contract and never reachable from a request
/// (ADR 0082).
/// <para>
/// It reads the <em>contact</em> address where its four siblings read the account's. The split is
/// ADR 0056's and it holds in both directions: a sanction is addressed to the person who signs in,
/// and a visitor's message to the address that person published for being written to.
/// </para>
/// <para>
/// Nothing the visitor typed is logged. What they wrote is theirs, the address they left is
/// personal data, and a log line is the one place in this system that keeps both for a year
/// (ADR 0026).
/// </para>
/// </remarks>
public sealed class SendContactMessageWhenTrainerContactedIntegrationEventHandler(
    ITrainerContactQuery contactQuery,
    ICatalogDetailQuery catalogDetail,
    IEmailSender emailSender,
    ILogger<SendContactMessageWhenTrainerContactedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<TrainerContactedIntegrationEvent>
{
    /// <inheritdoc />
    public string ConsumerName => "SendContactMessage";

    /// <summary>
    /// Runs the reaction to a delivered fact.
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task HandleAsync(
        TrainerContactedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var trainer = await contactQuery.GetByTrainerIdAsync(integrationEvent.TrainerId, cancellationToken);

        if (trainer is null)
        {
            // Nothing to retry against: a trainer who no longer exists is not a transient failure,
            // and throwing would spend this consumer's whole budget re-reading the same absence
            // (ADR 0033). The identifier alone is logged — never the message or its sender.
            logger.LogWarning(
                "A visitor wrote to trainer {TrainerId}, who no longer exists.",
                integrationEvent.TrainerId);

            return;
        }

        var about = await AboutAsync(integrationEvent.TrainingId, cancellationToken);
        var sender = $"{integrationEvent.SenderFirstname} {integrationEvent.SenderLastname}";

        var message = new EmailMessage(
            trainer.EmailAddress,
            about is null
                ? $"{sender} contacted you through TrainingHub"
                : $"{sender} contacted you about “{about}”",
            $"Hello {trainer.Firstname} {trainer.Lastname},\n\n"
            + $"{sender} <{integrationEvent.SenderEmailAddress}> wrote to you"
            + (about is null ? string.Empty : $" about your training “{about}”")
            + ":\n\n"
            + integrationEvent.Message
            + "\n\n"
            + "Replying to this message answers them directly. They were never shown your address.",
            ReplyTo: integrationEvent.SenderEmailAddress);

        await emailSender.SendAsync(message, cancellationToken);
    }

    /// <summary>
    /// The title of the training the visitor was reading, or <see langword="null"/> when they wrote
    /// from the trainer's own page or the training has since left the catalog.
    /// </summary>
    /// <remarks>
    /// Read through the catalog's own port rather than a repository, which is what a consumer may
    /// hold (ADR 0056), and read at delivery rather than carried on the fact, which is what keeps
    /// the envelope to what the visitor actually said. A training withdrawn between the sending and
    /// the delivery simply loses its mention: the message is still the visitor's and still worth
    /// carrying.
    /// </remarks>
    private async Task<string?> AboutAsync(Guid? trainingId, CancellationToken cancellationToken)
    {
        if (trainingId is not { } identifier)
        {
            return null;
        }

        var training = await catalogDetail.FindOfferedAsync(identifier, cancellationToken);

        return training?.Title;
    }
}
