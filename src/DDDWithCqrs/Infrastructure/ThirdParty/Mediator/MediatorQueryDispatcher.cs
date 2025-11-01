using BLRefactoring.Shared.CQS;
using Mediator;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.Mediator;

/// <summary>
/// Implements <see cref="IQueryDispatcher"/> using MediatR.
/// </summary>
public class MediatorQueryDispatcher : IQueryDispatcher
{
    private readonly IMediator _mediator;

    public MediatorQueryDispatcher(IMediator mediator) => _mediator = mediator;

    public ValueTask<TResult> DispatchAsync<TResult>(Shared.CQS.IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        return _mediator.Send(query, cancellationToken);
    }
}
