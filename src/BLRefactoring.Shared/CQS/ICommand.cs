using BLRefactoring.Shared.Common.Results;
using Mediator;

namespace BLRefactoring.Shared.CQS;

/// <summary>
/// Marks a command, whatever it answers.
/// </summary>
/// <remarks>
/// There used to be a non-generic <c>ICommand</c> beside <see cref="ICommand{TResult}"/>, for
/// commands returning nothing. No command in either stack ever implemented it: a command that
/// answers nothing still has to say whether it succeeded, so every one of them returns
/// <see cref="Result"/> and reaches for the generic form.
/// </remarks>
public interface ICommandBase : IMessage
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
