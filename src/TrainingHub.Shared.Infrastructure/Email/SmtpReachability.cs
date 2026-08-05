using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;

namespace TrainingHub.Shared.Infrastructure.Email;

/// <summary>
/// Proves the mail relay reachable with one connect-and-quit, never authenticating.
/// </summary>
/// <remarks>
/// The connection settings mirror <see cref="SmtpEmailSender"/> line for line, so what the probe
/// reaches is what a send would reach. Authentication is left out on purpose: reachability is the
/// question, and spending an authentication per poll against a relay that rate-limits failed
/// logins turns the probe itself into an outage (ADR 0037). Exceptions travel to the caller
/// untouched — the health check translates a fault into a status.
/// </remarks>
internal sealed class SmtpReachability(IOptions<SmtpOptions> options) : IEmailTransportReachability
{
    private readonly SmtpOptions _options = options.Value;

    /// <inheritdoc />
    public async Task ProbeAsync(CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient();

        await client.ConnectAsync(
            _options.Host,
            _options.Port,
            _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
            cancellationToken);

        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
