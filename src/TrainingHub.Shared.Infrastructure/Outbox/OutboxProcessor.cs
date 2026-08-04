using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace TrainingHub.Shared.Infrastructure.Outbox;

/// <summary>
/// Delivers one claimed batch of outbox messages: claim under a lease, dispatch, record the
/// outcome on each envelope.
/// </summary>
/// <remarks>
/// The claim is a single <c>UPDATE … OUTPUT</c> against the unprocessed rows, oldest first, and it
/// is what makes several workers — one per host, as ADR 0002 promised — safe against each other:
/// <c>READPAST</c> makes competing claimants skip each other's locked rows instead of queueing on
/// them, and the lease written by the claim (<c>ClaimedUntil</c>) keeps the claim owned across the
/// batch, so a worker that dies mid-delivery merely lets its lease lapse and the rows return to
/// the pool. Each message's outcome is saved as it happens, not at the end of the batch: a crash
/// then re-delivers at most the message in flight, which is the at-least-once contract consumers
/// already signed up for — their deduplication key is the envelope's id (ADR 0024, ADR 0025).
/// </remarks>
public sealed class OutboxProcessor(
    TrainingContext trainingContext,
    IntegrationEventDispatcher dispatcher,
    TimeProvider timeProvider,
    IOptions<OutboxOptions> options)
{
    /// <summary>
    /// Claims and delivers at most one batch. Answers how many messages were claimed, so the
    /// caller knows whether the table may hold more.
    /// </summary>
    /// <param name="claimant">Who is claiming — recorded on each row as provenance.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task<int> ProcessBatchAsync(string claimant, CancellationToken cancellationToken)
    {
        var configured = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var leaseEnd = now + configured.LeaseDuration;

        // One statement claims and reads: rows come back already leased to this worker, and rows
        // another worker holds are skipped (READPAST), not waited on. Raw SQL because the claim is
        // the one query EF cannot say: UPDATE TOP … OUTPUT with locking hints.
        var claimed = await trainingContext.Set<OutboxMessage>()
            .FromSqlInterpolated($@"
WITH claimable AS (
    SELECT TOP ({configured.BatchSize}) *
    FROM OutboxMessage WITH (UPDLOCK, READPAST, ROWLOCK)
    WHERE ProcessedOnUtc IS NULL
      AND Attempts < {configured.MaxAttempts}
      AND (ClaimedUntil IS NULL OR ClaimedUntil < {now})
    ORDER BY OccurredOnUtc
)
UPDATE claimable
SET ClaimedBy = {claimant}, ClaimedUntil = {leaseEnd}
OUTPUT inserted.*")
            .ToListAsync(cancellationToken);

        foreach (var message in claimed.OrderBy(message => message.OccurredOnUtc))
        {
            try
            {
                var fact = IntegrationEventSerializer.Deserialize(message.Name, message.Version, message.Payload);
                await dispatcher.DispatchAsync(fact, cancellationToken);
                message.MarkProcessed(timeProvider.GetUtcNow().UtcDateTime);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The broad catch is the retry mechanism, not a shrug: whatever a consumer threw
                // is recorded on the envelope, the attempt is counted, and the message returns to
                // the pool until the budget in MaxAttempts is spent.
                message.RecordFailure(exception.ToString());
            }

            // Saved per message rather than per batch: an outcome, once known, survives whatever
            // the next message does to this process.
            await trainingContext.SaveChangesAsync(cancellationToken);
        }

        return claimed.Count;
    }
}
