using TrainingHub.Shared.Common.Results;

namespace TrainingHub.Shared.CQS;

/// <summary>
/// Represents a command dispatcher.
/// </summary>
public interface ICommandDispatcher
{
    /// <summary>
    /// Dispatches asynchronously a command and returns a <see cref="Result"/>.
    /// </summary>
    ValueTask<TResult> DispatchAsync<TResult>(
        ICommand<TResult> command,
        CancellationToken cancellationToken = default)
        where TResult : Result;
}
