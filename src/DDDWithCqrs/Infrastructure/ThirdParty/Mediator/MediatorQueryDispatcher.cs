using BLRefactoring.Shared.CQS;
using Mediator;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.Mediator;

/// <summary>
/// Implements <see cref="IQueryDispatcher"/> using MediatR.
/// </summary>
public class MediatorQueryDispatcher(IMediator mediator) : IQueryDispatcher
{
    public ValueTask<TResult> DispatchAsync<TResult>(Shared.CQS.IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        return mediator.Send(query, cancellationToken);
    }
}
