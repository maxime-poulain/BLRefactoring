using TrainingHub.Shared.Application.Dtos.Outbox;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.Common.Results;

namespace TrainingHub.Shared.Application.Outbox;

/// <summary>
/// The outbox's operator surface: read what delivery gave up on, and hand one message back.
/// </summary>
/// <remarks>
/// ADR 0025 deferred this — <em>"a dead-letter surface (an endpoint, a metric, an alert) is
/// deliberately not designed here; the day it is wanted, the rows it would read already exist"</em> —
/// ADR 0033 deferred it again, and ADR 0037 built the pollable gauge while saying plainly it was
/// still not the endpoint. ADR 0061 is the day.
/// <para>
/// Two operations, and the pair is closed on purpose. There is no discard, no delete and no way to
/// edit a payload: a poison row is the evidence of a fact the system committed and then failed to
/// tell anybody about, and the only honest answers are "try again" and "leave it there". Anything
/// that removed it would let the mechanism destroy its own crime scene, which is the argument
/// ADR 0033 already makes about the retention sweep.
/// </para>
/// <para>
/// Declared here and implemented in the infrastructure, like every other read model's port: the
/// envelope is a persistence shape and never crosses this boundary, so what an operator reads is a
/// DTO built where the table is.
/// </para>
/// </remarks>
public interface IOutboxOperations
{
    /// <summary>
    /// The page of messages whose retry budget is spent and that are still undelivered.
    /// </summary>
    /// <param name="paging">The page asked for, under the published cap (ADR 0029).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The page, in the order the worker would have delivered them — oldest fact first — and the
    /// count of everything poisoned, so the envelope says how much there is to deal with.
    /// </returns>
    Task<PagedResult<PoisonedMessageDto>> GetPoisonedAsync(
        PageRequest paging,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts one poison message back in the pool: fresh budget, no schedule, no lease.
    /// </summary>
    /// <remarks>
    /// The delivery ledger is untouched, which is what makes this safe to press twice: the retry
    /// runs the consumers still owed and skips the ones that already succeeded (ADR 0034).
    /// </remarks>
    /// <param name="messageId">The envelope to requeue.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// Success when the message is back in the pool; <see cref="Common.Errors.ErrorCodes.NotFound"/>
    /// when no such message exists, and <see cref="OutboxErrorCodes.NotPoison"/> when the one named
    /// is still owed or already delivered.
    /// </returns>
    Task<Result> RequeueAsync(Guid messageId, CancellationToken cancellationToken = default);
}
