using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace BLRefactoring.Shared.Api.Authorization;

/// <summary>
/// Decides the <see cref="TrainingOwnerPolicy"/>: the caller must be the trainer who owns
/// the training named in the route.
/// </summary>
/// <remarks>
/// Shared by both hosts. It used to exist as two identical copies, one per API project, which
/// is the worst possible arrangement for a rule of this kind: a fix to the ownership check on
/// one host leaves the other exposed, and nothing about the code says so.
/// </remarks>
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
        //
        // RequestAborted rather than nothing: this was the one database call in the solution that
        // did not propagate a token, so a caller who hung up left it running to completion — on
        // the authorization path, which every guarded write goes through.
        var training = await trainingRepository.GetByIdAsync(
            TrainingId.Create(trainingId), httpContext.RequestAborted);
        if (training is null || training.TrainerId.Value == trainerIdFromToken)
        {
            context.Succeed(requirement);
        }
    }
}
