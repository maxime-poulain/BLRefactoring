namespace BLRefactoring.Shared;

/// <summary>
/// Outbound port for sending emails, consumed by application-layer code such as
/// domain event handlers. Implementations live in the infrastructure layer.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends the given <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The email to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// The email to send through an <see cref="IEmailSender"/>.
/// </summary>
/// <param name="Recipient">The address the email is sent to.</param>
/// <param name="Subject">The subject line.</param>
/// <param name="Body">The plain-text body.</param>
public sealed record EmailMessage(string Recipient, string Subject, string Body);
