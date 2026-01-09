namespace BLRefactoring.Shared;

/// <summary>
/// Represents a transaction manager that provides transaction management capabilities.
/// Enables asynchronous transaction handling with support for begin, commit, and rollback operations.
/// </summary>
public interface ITransactionManager
{
    /// <summary>
    /// Begins a new transaction asynchronously.
    /// </summary>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the operation.
    /// Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction asynchronously, persisting all changes made during the transaction.
    /// </summary>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the operation.
    /// Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction asynchronously, discarding all changes made during the transaction.
    /// </summary>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the operation.
    /// Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RollBackAsync(CancellationToken cancellationToken = default);
}
