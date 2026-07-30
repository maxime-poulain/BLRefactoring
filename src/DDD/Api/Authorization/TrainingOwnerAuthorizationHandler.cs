using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using Microsoft.AspNetCore.Authorization;

namespace BLRefactoring.DDD.Api.Authorization;

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

        var trainerIdClaim = httpContext.User.FindFirst("trainer_id")?.Value;
        if (trainerIdClaim is null || !Guid.TryParse(trainerIdClaim, out var trainerIdFromToken))
        {
            return;
        }

        // This policy only guards ownership; existence is the action's concern.
        // A nonexistent training therefore succeeds the requirement so the
        // action can answer 404 — failing here would turn "not found" into an
        // incorrect 403. Ownership of a training is not a secret anyway: every
        // authenticated caller can list all trainings.
        var training = await trainingRepository.GetByIdAsync(TrainingId.Create(trainingId));
        if (training is null || training.TrainerId.Value == trainerIdFromToken)
        {
            context.Succeed(requirement);
        }
    }
}
