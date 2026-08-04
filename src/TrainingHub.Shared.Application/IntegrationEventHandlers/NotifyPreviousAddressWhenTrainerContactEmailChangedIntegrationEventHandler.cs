using TrainingHub.Shared.Application.IntegrationEvents;

namespace TrainingHub.Shared.Application.IntegrationEventHandlers;

/// <summary>
/// Warns the address a trainer just moved away from — the security policy, running where it always
/// belonged: after the change is real.
/// </summary>
/// <remarks>
/// The warning gives the legitimate owner a chance to react to a change that was not theirs, which
/// only makes sense for a change that committed — warning about a rolled-back edit would be noise
/// at best and a phishing tutor at worst. The fact carries both addresses because the aggregate has
/// long forgotten the old one by the time the worker delivers.
/// </remarks>
public sealed class NotifyPreviousAddressWhenTrainerContactEmailChangedIntegrationEventHandler(IEmailSender emailSender)
    : IIntegrationEventHandler<TrainerContactEmailChangedIntegrationEvent>
{
    /// <summary>
    /// Runs the reaction to a delivered fact.
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task HandleAsync(TrainerContactEmailChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var message = new EmailMessage(
            integrationEvent.OldContactEmail,
            "Your contact email address was changed",
            $"The contact email address of your trainer profile was changed to {integrationEvent.NewContactEmail}. " +
            "If you did not request this change, please contact support immediately.");

        await emailSender.SendAsync(message, cancellationToken);
    }
}
