namespace BLRefactoring.Shared.CQS;

/// <summary>
/// Represents a query dispatcher.
/// </summary>
public interface IQueryDispatcher
{
    /// <summary>
    /// Dispatches a query and returns the result.
    /// </summary>
    /// <typeparam name="TResult">The type of the query execution result.</typeparam>
    /// <param name="query">Query to operate on</param>
    /// <returns>The result of processing the query.</returns>
    public Task<TResult> DispatchAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}
