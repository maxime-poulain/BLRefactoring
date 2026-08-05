using Microsoft.Extensions.Diagnostics.HealthChecks;
using TrainingHub.Shared.Infrastructure.Storage;

namespace TrainingHub.Shared.Api.Health;

/// <summary>
/// Answers whether the object store answers, through the reachability port.
/// </summary>
/// <remarks>
/// The port exists because only the infrastructure may name the AWS SDK; what it runs is one
/// signed, single-key list of the bucket (ADR 0037). A probe that throws is left to throw:
/// <c>HealthCheckService</c> catches it and answers the registration's failure status, and the
/// response writer never emits the exception it caught.
/// </remarks>
internal sealed class ObjectStoreHealthCheck(IObjectStoreReachability reachability) : IHealthCheck
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
