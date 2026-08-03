namespace TrainingHub.Shared;

/// <summary>
/// Represents the unit of work of the current request: the single point where
/// pending changes staged by the repositories are persisted.
/// </summary>
/// <remarks>
/// <para>
/// Repositories only mutate the underlying change tracker (Add/Update/Delete);
/// nothing hits the database until <see cref="SaveChangesAsync"/> is called by the
/// command handler or application service orchestrating the use case, exactly once.
/// </para>
/// <para>
/// The contract is identical to <c>DbContext.SaveChangesAsync</c>: all pending
/// changes are persisted atomically within an implicit database transaction, and
/// the returned value is the number of state entries written to the database.
/// Domain events raised by the tracked aggregates are dispatched during this call,
/// before persistence, so that changes made by their handlers are part of the same
/// transaction.
/// </para>
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// Persists all pending changes to the database within a single implicit transaction.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
