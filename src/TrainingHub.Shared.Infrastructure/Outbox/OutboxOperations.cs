using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrainingHub.Shared.Application.Dtos.Outbox;
using TrainingHub.Shared.Application.Outbox;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.Infrastructure.Pagination;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Outbox;

namespace TrainingHub.Shared.Infrastructure.Outbox;

/// <inheritdoc />
public sealed class OutboxOperations(
    TrainingContext trainingContext,
    TimeProvider timeProvider,
    IOptions<OutboxOptions> options,
    ILogger<OutboxOperations> logger) : IOutboxOperations
{
    /// <inheritdoc />
    /// <remarks>
    /// The predicate is the claim query's own exclusion, read the other way round: the worker takes
    /// rows with <c>Attempts &lt; MaxAttempts</c>, so what it will never take again is exactly this.
    /// The budget is read from the same options the worker reads, because a poison row is defined by
    /// a configured number and not by a constant somebody copied.
    /// <para>
    /// Two statements rather than one join, so the page's rows are settled before the ledger is
    /// asked about them: an <c>IN</c> over at most one page of identifiers, instead of a query per
    /// row. The projection is applied by the paging helper, so SQL selects the columns the DTO
    /// needs — the payload column, which this surface does not publish, is never read.
    /// </para>
    /// </remarks>
    public async Task<PagedResult<PoisonedMessageDto>> GetPoisonedAsync(
        PageRequest paging,
        CancellationToken cancellationToken = default)
    {
        var budget = options.Value.MaxAttempts;

        var page = await trainingContext.Set<OutboxMessage>()
            .AsNoTracking()
            .Where(message => message.ProcessedOnUtc == null && message.Attempts >= budget)
            // The order the worker would have delivered them in, which is the order an operator
            // reads a backlog in: oldest fact first, the identifier settling the ties (ADR 0029).
            .OrderBy(message => message.OccurredOnUtc)
            .ThenBy(message => message.Id)
            .ToPagedResultAsync(
                message => new PoisonedMessageRow(
                    message.Id,
                    message.Name,
                    message.Version,
                    message.OccurredOnUtc,
                    message.Attempts,
                    message.Error,
                    message.RequeuedOnUtc),
                paging,
                cancellationToken);

        var identifiers = page.Items.Select(message => message.Id).ToList();

        var settled = await trainingContext.Set<OutboxMessageConsumer>()
            .AsNoTracking()
            .Where(delivery => identifiers.Contains(delivery.MessageId))
            .Select(delivery => new { delivery.MessageId, delivery.ConsumerName })
            .ToListAsync(cancellationToken);

        var byMessage = settled
            .GroupBy(delivery => delivery.MessageId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)[
                    .. group.Select(delivery => delivery.ConsumerName).Order(StringComparer.Ordinal)]);

        var described = page.Items
            .Select(message => new PoisonedMessageDto
            {
                Id = message.Id,
                Name = message.Name,
                Version = message.Version,
                OccurredOnUtc = message.OccurredOnUtc,
                Attempts = message.Attempts,
                Error = message.Error,
                RequeuedOnUtc = message.RequeuedOnUtc,
                DeliveredConsumers = byMessage.TryGetValue(message.Id, out var consumers) ? consumers : []
            })
            .ToList();

        return new PagedResult<PoisonedMessageDto>(described, page.Page, page.PageSize, page.TotalCount);
    }

    /// <summary>
    /// The columns one poison row contributes, before the ledger is asked what it already reached.
    /// </summary>
    /// <remarks>
    /// A shape that exists for the length of one method, so the projection EF translates names
    /// exactly the columns it reads and the DTO is never built half-empty. Private, because nothing
    /// outside this adapter has a reason to know the read happens in two statements.
    /// </remarks>
    private sealed record PoisonedMessageRow(
        Guid Id,
        string Name,
        int Version,
        DateTime OccurredOnUtc,
        int Attempts,
        string? Error,
        DateTime? RequeuedOnUtc);

    /// <inheritdoc />
    /// <remarks>
    /// Tracked, not an <c>ExecuteUpdate</c>: the transition belongs to the envelope, which is where
    /// the invariant that a requeue keeps its history lives. A set-based update would write the same
    /// columns while stepping around the one type that knows what they mean together.
    /// <para>
    /// The ledger is not touched, and that is the operation's whole safety. Its rows are what let
    /// the retry skip the consumers that already succeeded, so a requeue is idempotent in the way
    /// that matters: pressing it twice does not send a welcome email twice (ADR 0034).
    /// </para>
    /// </remarks>
    public async Task<Result> RequeueAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var message = await trainingContext.Set<OutboxMessage>()
            .FirstOrDefaultAsync(candidate => candidate.Id == messageId, cancellationToken);

        if (message is null)
        {
            return Result.Failure(ErrorCodes.NotFound, $"No outbox message with identifier '{messageId}' exists.");
        }

        if (message.ProcessedOnUtc is not null || message.Attempts < options.Value.MaxAttempts)
        {
            return Result.Failure(
                OutboxErrorCodes.NotPoison,
                message.ProcessedOnUtc is not null
                    ? "The message was delivered and cannot be requeued."
                    : "The message still has attempts left and is already scheduled for another try.");
        }

        // Read before the transition, which is the point of the line: after it, the counter is
        // zero and the number an operator wants to see in the log is gone.
        var spent = message.Attempts;

        message.Requeue(timeProvider.GetUtcNow().UtcDateTime);

        await trainingContext.SaveChangesAsync(cancellationToken);

        // Information, and said here rather than left to the worker: the retry logs only if it
        // fails again, so without this line the only record of the decision would be the row.
        logger.LogInformation(
            "Outbox message {MessageId} ({Name} v{Version}) was requeued after {Attempts} failed attempts.",
            message.Id,
            message.Name,
            message.Version,
            spent);

        return Result.Success();
    }
}
