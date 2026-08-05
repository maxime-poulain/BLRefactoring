using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TrainingHub.Shared.Infrastructure.Outbox;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Outbox;

namespace TrainingHub.Shared.Api.Health;

/// <summary>
/// Counts the outbox rows whose retry budget is spent, and answers Degraded while any exist.
/// </summary>
/// <remarks>
/// The pollable half of ADR 0033's log line: poison is operator evidence that halts nothing, so
/// this check never answers Unhealthy — the host still serves, and failing readiness over it would
/// take traffic away from a process that is fine. Degraded is what makes the evidence reachable by
/// a poll instead of a log search; the dead-letter surface with a URL — a list, a requeue — stays
/// deferred exactly as ADRs 0025 and 0033 left it (ADR 0037). The count travels in the result's
/// description for a debugger attached to the report, and nowhere else: the response writer emits
/// names and statuses only.
/// </remarks>
internal sealed class OutboxPoisonHealthCheck(
    TrainingContext trainingContext,
    IOptions<OutboxOptions> options) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var configured = options.Value;

        var poisoned = await trainingContext.Set<OutboxMessage>()
            .CountAsync(
                message => message.ProcessedOnUtc == null && message.Attempts >= configured.MaxAttempts,
                cancellationToken);

        return poisoned == 0
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Degraded($"{poisoned} poison message(s) sit in the outbox awaiting an operator.");
    }
}
