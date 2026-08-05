namespace TrainingHub.Shared.Infrastructure.Email;

/// <summary>
/// Answers whether the mail relay is reachable, for the readiness probe alone.
/// </summary>
/// <remarks>
/// A port with one consumer: the health check in <c>Shared.Api</c>. It exists because MailKit may
/// only be named by the infrastructure (ADR 0031's confinement rule), and it is deliberately not
/// a second method on <see cref="TrainingHub.Shared.Application.Notifications.IEmailSender"/> —
/// that port is the application's sending vocabulary, and "are you there?" is an operational
/// question, not a notification (ADR 0037).
/// </remarks>
public interface IEmailTransportReachability
{
    /// <summary>
    /// Opens and closes one connection to the relay, throwing when it cannot be completed.
    /// </summary>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>A task that completes when the relay answered, and faults when it did not.</returns>
    Task ProbeAsync(CancellationToken cancellationToken = default);
}
