using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using Mediator;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.MediatR;

/// <summary>
/// Implements <see cref="ICommandDispatcher"/> using MediatR.
/// </summary>
public class MediatorCommandDispatcher(IMediator mediator) : ICommandDispatcher
{
    /// <inheritdoc/>
    public ValueTask<TResult> DispatchAsync<TResult>(Shared.CQS.ICommand<TResult> command, CancellationToken cancellationToken = default) where TResult : Result
    {
        return mediator.Send(command, cancellationToken);
    }
}
