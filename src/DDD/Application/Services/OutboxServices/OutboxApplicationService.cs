using TrainingHub.Shared.Application.Dtos.Outbox;
using TrainingHub.Shared.Application.Outbox;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.Common.Results;

namespace TrainingHub.DDD.Application.Services.OutboxServices;

/// <summary>
/// Application service interface for the outbox's operator surface: what delivery gave up on, and
/// the one way back in.
/// </summary>
/// <remarks>
/// The second application service here that drives no aggregate, and for a different reason than
/// <c>ICatalogApplicationService</c>: that one reads a read model, this one reads and writes the
/// platform's own delivery table. Neither has a rule to break, which is why the read answers no
/// <c>Result</c> and the write answers a bare one (ADR 0061).
/// </remarks>
public interface IOutboxApplicationService
{
    /// <summary>
    /// One page of the messages whose retry budget is spent and that are still undelivered.
    /// </summary>
    /// <param name="paging">The page asked for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<PagedResult<PoisonedMessageDto>> GetPoisonedAsync(
        PageRequest paging,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts one poison message back in the pool.
    /// </summary>
    /// <param name="messageId">The message the caller named.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<Result> RequeueAsync(Guid messageId, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
/// <remarks>
/// A pass-through, for the reason <c>CatalogApplicationService</c> states and which applies twice
/// over here: there is no aggregate to drive, so the two stacks would be inventing a difference
/// rather than expressing one. Both arrive at the same port, which is where the decision — is this
/// row poison, and what does putting it back mean — actually lives.
/// </remarks>
public sealed class OutboxApplicationService(IOutboxOperations outboxOperations) : IOutboxApplicationService
{
    /// <inheritdoc />
    public async Task<PagedResult<PoisonedMessageDto>> GetPoisonedAsync(
        PageRequest paging,
        CancellationToken cancellationToken = default) =>
        await outboxOperations.GetPoisonedAsync(paging, cancellationToken);

    /// <inheritdoc />
    public async Task<Result> RequeueAsync(Guid messageId, CancellationToken cancellationToken = default) =>
        await outboxOperations.RequeueAsync(messageId, cancellationToken);
}
