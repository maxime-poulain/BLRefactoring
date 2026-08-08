using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using TrainingHub.Shared.Api.Identity;
using TrainingHub.Shared.Application.Queries;

namespace TrainingHub.Shared.Api.Authorization;

/// <summary>
/// Decides the <see cref="ActiveTrainerPolicy"/>: the caller must not be under suspension.
/// </summary>
/// <remarks>
/// Shared by both hosts, like the ownership handler beside it and for the same reason: a lockout
/// fixed on one host and forgotten on the other is the worst possible arrangement for a rule of
/// this kind.
/// <para>
/// The standing is read on every guarded write rather than taken from the token, and that is the
/// point. The token carries no standing and could not usefully carry one: it is minted at sign-in,
/// there is no refresh token, so a suspension decided while the trainer is signed in would not
/// reach a claim until they signed in again — which is exactly the window in which the sanction
/// has to hold.
/// </para>
/// </remarks>
public sealed class ActiveTrainerAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    ITrainerStandingQuery trainerStandingQuery)
    : AuthorizationHandler<ActiveTrainerRequirement>
{
    /// <summary>
    /// Grants the requirement unless the caller is a trainer under suspension.
    /// </summary>
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveTrainerRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        var trainerIdClaim = httpContext.User.FindFirst(TrainerClaims.TrainerId)?.Value;
        if (trainerIdClaim is null || !Guid.TryParse(trainerIdClaim, out var trainerId))
        {
            // Nobody's trainer, so nobody this policy has anything to say about. TrainerPolicy is
            // what refuses them, and it refuses them on every action of the surface rather than on
            // the writes alone — failing here as well would answer the same caller twice.
            context.Succeed(requirement);
            return;
        }

        if (!await trainerStandingQuery.IsSuspendedAsync(trainerId, httpContext.RequestAborted))
        {
            context.Succeed(requirement);
        }
    }
}
