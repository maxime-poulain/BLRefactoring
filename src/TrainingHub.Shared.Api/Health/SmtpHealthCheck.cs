using Microsoft.Extensions.Diagnostics.HealthChecks;
using TrainingHub.Shared.Infrastructure.Email;

namespace TrainingHub.Shared.Api.Health;

/// <summary>
/// Answers whether the mail relay answers, through the reachability port.
/// </summary>
/// <remarks>
/// The port exists because only the infrastructure may speak SMTP; what it runs is one
/// connect-and-quit that never authenticates (ADR 0037). A probe that throws is left to throw:
/// <c>HealthCheckService</c> catches it and answers the registration's failure status, and the
/// response writer never emits the exception it caught.
/// </remarks>
internal sealed class SmtpHealthCheck(IEmailTransportReachability reachability) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await reachability.ProbeAsync(cancellationToken);

        return HealthCheckResult.Healthy();
    }
}
