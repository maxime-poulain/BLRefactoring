namespace TrainingHub.Shared.Application.Notifications;

/// <summary>
/// Outbound port for sending emails, declared beside its only consumers — the integration
/// event handlers of this layer. Implementations live in the infrastructure layer.
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
/// <remarks>
/// The four notices this application sends on its own behalf leave <paramref name="ReplyTo"/> unset
/// and are answered at the sender address the options declare. The one message it forwards on
/// somebody else's behalf sets it, because the person who should receive the answer is not the
/// person who sent it (ADR 0082).
/// </remarks>
/// <param name="Recipient">The address the email is sent to.</param>
/// <param name="Subject">The subject line.</param>
/// <param name="Body">The plain-text body.</param>
/// <param name="ReplyTo">
/// The address an answer should go to, or <see langword="null"/> to let it come back here.
/// </param>
public sealed record EmailMessage(string Recipient, string Subject, string Body, string? ReplyTo = null);
