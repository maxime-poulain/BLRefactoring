using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using MediatR;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.MediatR;

/// <summary>
/// Implements <see cref="ICommandDispatcher"/> using MediatR.
/// </summary>
public class MediatRCommandDispatcher : ICommandDispatcher
{
    private readonly IMediator _mediator;

    public MediatRCommandDispatcher(IMediator mediator) => _mediator = mediator;

    /// <inheritdoc/>
    public Task<TResult> DispatchAsync<TResult>(
        ICommand<TResult> command,
        CancellationToken cancellationToken = default)
        where TResult : Result
    {
        return _mediator.Send(command, cancellationToken);
    }
}
