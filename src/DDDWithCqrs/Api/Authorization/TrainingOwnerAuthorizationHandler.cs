using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using Microsoft.AspNetCore.Authorization;

namespace BLRefactoring.DDDWithCqrs.Api.Authorization;

public class TrainingOwnerAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    ITrainingRepository trainingRepository)
    : AuthorizationHandler<TrainingOwnerRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TrainingOwnerRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        if (!httpContext.Request.RouteValues.TryGetValue("trainingId", out var routeValue)
            || !Guid.TryParse(routeValue?.ToString(), out var trainingId))
        {
            return;
        }

        var training = await trainingRepository.GetByIdAsync(TrainingId.Create(trainingId));
        if (training is null)
        {
            return;
        }

        var trainerIdClaim = httpContext.User.FindFirst("trainer_id")?.Value;
        if (trainerIdClaim is null || !Guid.TryParse(trainerIdClaim, out var trainerIdFromToken))
        {
            return;
        }

        if (training.TrainerId.Value == trainerIdFromToken)
        {
            context.Succeed(requirement);
        }
    }
}
