using BLRefactoring.Shared.Common.Results;

namespace BLRefactoring.Shared.CQS;

/// <summary>
/// Represents a command dispatcher.
/// </summary>
public interface ICommandDispatcher
{
    /// <summary>
    /// Dispatches asynchronously a command and returns a <see cref="Result"/>.
    /// </summary>
    public ValueTask<TResult> DispatchAsync<TResult>(
        ICommand<TResult> command,
        CancellationToken cancellationToken = default)
        where TResult : Result;
}
