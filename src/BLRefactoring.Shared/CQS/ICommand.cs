using BLRefactoring.Shared.Common.Results;
using Mediator;

namespace BLRefactoring.Shared.CQS;

/// <summary>
/// Represents a marker interface for commands returning a <see cref="Result"/> or not.
/// </summary>
public interface ICommandBase : IMessage
{
}

/// <summary>
/// Represents a command that returns no data.
/// </summary>
public interface ICommand : ICommandBase, IMessage
{
}

/// <summary>
/// Represents a command that returns a <see cref="Result"/>.
/// </summary>
/// <typeparam name="TResult">The type of the result returned by the command.</typeparam>
public interface ICommand<out TResult> : ICommandBase, IRequest<TResult>
    where TResult : Result
{
}

/// <summary>
/// Represents the handler for a command that returns a <see cref="Result"/>.
/// </summary>
/// <typeparam name="TCommand">The type of the command being handled.</typeparam>
/// <typeparam name="TResult">The type of the result returned by the command.</typeparam>
public interface ICommandHandler<in TCommand, TResult> : IRequestHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
    where TResult : Result
{
}
