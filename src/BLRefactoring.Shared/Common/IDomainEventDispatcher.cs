namespace BLRefactoring.Shared.Common;

/// <summary>
/// Dispatches a batch of <see cref="IDomainEvent"/> to their in-process handlers.
/// </summary>
/// <remarks>
/// <para>
/// Aggregates accumulate domain events while a use case executes; the infrastructure
/// collects those events (and clears them from their aggregates) at save time, then
/// hands the whole batch to this dispatcher. Events are dispatched in collection order,
/// before the changes are persisted, so any modification performed by a handler on a
/// tracked entity is persisted within the same transaction as the original changes.
/// </para>
/// <para>
/// The batch-oriented contract keeps the abstraction independent from both the
/// aggregates and the persistence layer, and allows alternative implementations such
/// as an outbox-based dispatcher that stores the batch transactionally for deferred
/// publication.
/// </para>
/// </remarks>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Dispatches the given domain events, in order, to their registered handlers.
    /// </summary>
    /// <param name="domainEvents">The batch of domain events to dispatch.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
