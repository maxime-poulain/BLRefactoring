using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.Identity;

namespace TrainingHub.Shared.Api.Health;

/// <summary>
/// Answers whether this host's schema is current: Unhealthy while either context has a migration
/// the database has not seen.
/// </summary>
/// <remarks>
/// The pollable half of what ADR 0003 could only log. Outside Development that record applies no
/// migration and writes one critical line naming the pending ones — addressed to a human who may
/// be reading, at a moment nobody is. An instance whose schema is behind then reported itself
/// ready, and the first request touching a missing column failed. Readiness is addressed to the
/// machine that is already asking every few seconds and acting on the answer, so the fact belongs
/// there too (ADR 0045).
/// <para>
/// Unhealthy rather than Degraded, unlike the outbox's poison gauge: poison is evidence about work
/// already done and halts nothing, while a missing column is a promise that requests touching those
/// tables will fail. Both contexts are read on every poll and nothing is cached — the schema is
/// applied out of band, so this has to go green on its own when it lands, or it would reintroduce
/// the restart ADR 0003 removed the need for.
/// </para>
/// <para>
/// The count travels in the description, for the dashboard and a debugger attached to the report.
/// The migration <em>names</em> do not: <c>/health/ready</c> emits names and statuses only (ADR
/// 0037), which is why the startup log stays exactly as it is. The probe says whether; the log says
/// which.
/// </para>
/// <para>
/// Public where its four siblings are internal, and for one reason: their whole behavior belongs
/// to the dependency they wrap, so an end-to-end poll proves them. This one composes two contexts
/// and a description, and its failing branch is the decision the record makes — a branch no
/// integration test can reach without mutilating the migration history the whole suite shares. So
/// it is reachable by a unit test instead.
/// </para>
/// </remarks>
public sealed class PendingMigrationsHealthCheck(
    TrainingContext trainingContext,
    TrainingIdentityDbContext identityContext) : IHealthCheck
{
    /// <summary>
    /// Reads the pending migrations of both contexts and reports whether any remain.
    /// </summary>
    /// <param name="context">The check's registration, as the framework supplies it.</param>
    /// <param name="cancellationToken">Cancels the two reads.</param>
    /// <returns>Healthy when both schemas are current, Unhealthy otherwise.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Both are read before either is judged: a report naming one context and staying silent
        // about the other would send an operator to apply half of what is missing.
        var pendingBusiness = await PendingCountAsync(trainingContext, cancellationToken);
        var pendingIdentity = await PendingCountAsync(identityContext, cancellationToken);

        if (pendingBusiness == 0 && pendingIdentity == 0)
        {
            return HealthCheckResult.Healthy();
        }

        return HealthCheckResult.Unhealthy(
            $"{nameof(TrainingContext)}: {pendingBusiness} pending migration(s); "
            + $"{nameof(TrainingIdentityDbContext)}: {pendingIdentity} pending migration(s). "
            + "This host does not apply them outside Development — see the startup log for their names.");
    }

    private static async Task<int> PendingCountAsync(DbContext context, CancellationToken cancellationToken) =>
        (await context.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
}
