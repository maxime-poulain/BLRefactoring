using Microsoft.Extensions.Diagnostics.HealthChecks;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;

namespace TrainingHub.Shared.Api.Health;

/// <summary>
/// Answers whether the database answers: one connection, opened and closed.
/// </summary>
/// <remarks>
/// <c>CanConnectAsync</c> is the whole probe — the EF health-check package exists to wrap this one
/// line in an extension method, which is why ADR 0037 declines it. The context arrives scoped:
/// <c>HealthCheckService</c> runs every check inside a DI scope of its own, so this borrows a
/// connection exactly the way a request would and holds nothing between polls.
/// </remarks>
internal sealed class SqlServerHealthCheck(TrainingContext trainingContext) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        await trainingContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The database did not answer a connection.");
}
