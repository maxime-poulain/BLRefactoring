using BLRefactoring.Shared.Common.Results;

namespace BLRefactoring.Shared.CQS;

/// <summary>
/// Represents a command dispatcher.
/// </summary>
public interface ICommandDispatcher
{
    /// <summary>
    /// Dispatches asynchronously a command and returns a <see cref="IResult"/>.
    /// </summary>
    public Task<TResult> DispatchAsync<TResult>(
        ICommand<TResult> command,
        CancellationToken cancellationToken = default)
        where TResult : IResult;
}
