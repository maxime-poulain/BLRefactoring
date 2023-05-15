using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.DDD.Infrastructure.Repositories.EfCore;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.DDDWithCqrs.Infrastructure.ThirdParty.MediatR.Behaviors;

/// <summary>
/// Disables EF Core tracking during query execution.
/// </summary>
public class NoTrackingDuringQueryExecutionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly TrainingContext _trainingContext;

    public NoTrackingDuringQueryExecutionBehavior(TrainingContext trainingContext)
    {
        _trainingContext = trainingContext;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IQuery)
        {
            return await next();
        }

        var originalTrackingBehavior = _trainingContext.ChangeTracker.QueryTrackingBehavior;
        try
        {
            _trainingContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            return await next();
        }
        finally
        {
            _trainingContext.ChangeTracker.QueryTrackingBehavior = originalTrackingBehavior;
        }
    }
}
