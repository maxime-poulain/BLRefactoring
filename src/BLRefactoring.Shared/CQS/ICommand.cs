using MediatR;

namespace BLRefactoring.Shared.CQS;

/// <summary>
/// Represents a marker interface for commands returning data or not.
/// </summary>
public interface ICommandBase : IBaseRequest
{
}

/// <summary>
/// Represents a command that returns no data.
/// </summary>
public interface ICommand : ICommandBase, IRequest
{
}

/// <summary>
/// Represents a command that returns data.
/// </summary>
/// <typeparam name="TResponse"></typeparam>
public interface ICommand<out TResponse> : ICommandBase, IRequest<TResponse>
{
}

/// <summary>
/// Represents the handler for a command that returns no data.
/// </summary>
/// <typeparam name="TCommand"></typeparam>
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand>
    where TCommand : ICommand
{
}

/// <summary>
/// Represents the handler for a command that returns data.
/// </summary>
/// <typeparam name="TCommand"></typeparam>
/// <typeparam name="TResponse"></typeparam>
public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
}
