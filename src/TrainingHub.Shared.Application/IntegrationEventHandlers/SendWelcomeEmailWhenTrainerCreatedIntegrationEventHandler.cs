using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Notifications;

namespace TrainingHub.Shared.Application.IntegrationEventHandlers;

/// <summary>
/// Welcomes a new trainer — the policy the outbox deferred, reattached on the consuming side.
/// </summary>
/// <remarks>
/// This is the reaction that used to run inside the transaction and motivated ADR 0002: an email
/// sent for a commit that could still fail. It now runs from the committed
/// <see cref="TrainerCreatedIntegrationEvent"/>, delivered by the worker, so every welcome message
/// answers a trainer that exists. Composing the message here — recipient, subject, wording — is
/// the consumer deciding what the fact means to it, which is what publishing facts instead of
/// intents bought (ADR 0024). The delivery ledger sends this once per fact even when a neighbor
/// fails (ADR 0034); the residual duplicate a lapsed lease can cause is an annoyance, not a
/// corruption, so this handler still carries no deduplication of its own.
/// </remarks>
public sealed class SendWelcomeEmailWhenTrainerCreatedIntegrationEventHandler(IEmailSender emailSender)
    : IIntegrationEventHandler<TrainerCreatedIntegrationEvent>
{
    /// <inheritdoc />
    public string ConsumerName => "SendWelcomeEmail";

    /// <summary>
    /// Runs the reaction to a delivered fact.
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task HandleAsync(TrainerCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var message = new EmailMessage(
            integrationEvent.ContactEmail,
            "Welcome aboard!",
            $"Hello {integrationEvent.Firstname} {integrationEvent.Lastname}, " +
            "your trainer account has been created. You can now publish your first training.");

        await emailSender.SendAsync(message, cancellationToken);
    }
}
