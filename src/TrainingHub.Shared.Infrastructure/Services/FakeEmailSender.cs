using Microsoft.Extensions.Logging;

namespace TrainingHub.Shared.Infrastructure.Services;

/// <summary>
/// Deliberately fake <see cref="IEmailSender"/> that only logs the email it was
/// asked to send. No SMTP server, no external provider: the purpose of this
/// showcase is the architecture around the port, not the delivery itself.
/// Swapping in a real implementation is a pure infrastructure change — nothing
/// in the application layer would move.
/// </summary>
public sealed class FakeEmailSender(ILogger<FakeEmailSender> logger) : IEmailSender
{
    /// <inheritdoc />
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Sending email to `{Recipient}` with subject `{Subject}`: {Body}",
            message.Recipient,
            message.Subject,
            message.Body);

        return Task.CompletedTask;
    }
}
