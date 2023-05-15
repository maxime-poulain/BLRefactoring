using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using MediatR;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.MediatR;

/// <summary>
/// Implements <see cref="IQueryDispatcher"/> using MediatR.
/// </summary>
public class MediatRQueryDispatcher : IQueryDispatcher
{
    private readonly IMediator _mediator;

    public MediatRQueryDispatcher(IMediator mediator) => _mediator = mediator;

    public Task<TResult> DispatchAsync<TResult>(
        IQuery<TResult> query,
        CancellationToken cancellationToken = default)
    {
        return _mediator.Send(query, cancellationToken);
    }
}
