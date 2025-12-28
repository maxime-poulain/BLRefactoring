using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.DDD.Infrastructure.Repositories.EfCore;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.Mediator.Behaviors;

/// <summary>
/// Disables EF Core tracking during query execution.
/// </summary>
public class NoTrackingDuringQueryExecutionBehavior<TRequest, TResponse>(
    TrainingContext trainingContext) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IMessage
{
    public async ValueTask<TResponse> Handle(
        TRequest request,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IQuery)
        {
            return await next(request, cancellationToken);
        }

        var originalTrackingBehavior = trainingContext.ChangeTracker.QueryTrackingBehavior;
        try
        {
            trainingContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            return await next(request, cancellationToken);
        }
        finally
        {
            trainingContext.ChangeTracker.QueryTrackingBehavior = originalTrackingBehavior;
        }
    }
}
