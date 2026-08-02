using BLRefactoring.Shared.CQS;
using Mediator;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.Mediator;

/// <summary>
/// Implements <see cref="IQueryDispatcher"/> using MediatR.
/// </summary>
public sealed class MediatorQueryDispatcher(IMediator mediator) : IQueryDispatcher
{
    /// <summary>
    /// Sends a query to its handler through the mediator.
    /// </summary>
    public ValueTask<TResult> DispatchAsync<TResult>(Shared.CQS.IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        return mediator.Send(query, cancellationToken);
    }
}
