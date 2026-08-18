using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Notifications;

namespace TrainingHub.Shared.Application.IntegrationEventHandlers;

/// <summary>
/// Warns the address a trainer just moved away from — the security policy, running where it always
/// belonged: after the change is real.
/// </summary>
/// <remarks>
/// The warning gives the legitimate owner a chance to react to a change that was not theirs, which
/// only makes sense for a change that committed — warning about a rolled-back edit would be noise
/// at best and a phishing tutor at worst. The fact carries both addresses because the aggregate has
/// long forgotten the old one by the time the worker delivers — and the language beside them,
/// because whoever reads this notice is resolved here and nowhere else (ADR 0091).
/// </remarks>
public sealed class NotifyPreviousAddressWhenTrainerContactEmailChangedIntegrationEventHandler(
    INotificationComposer composer,
    IEmailSender emailSender)
    : IIntegrationEventHandler<TrainerContactEmailChangedIntegrationEvent>
{
    /// <inheritdoc />
    public string ConsumerName => "NotifyPreviousAddress";

    /// <summary>
    /// Runs the reaction to a delivered fact.
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task HandleAsync(TrainerContactEmailChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var notification = composer.ContactEmailChanged(
            integrationEvent.Language,
            integrationEvent.NewContactEmail);

        var message = new EmailMessage(
            integrationEvent.OldContactEmail,
            notification.Subject,
            notification.Body);

        await emailSender.SendAsync(message, cancellationToken);
    }
}
