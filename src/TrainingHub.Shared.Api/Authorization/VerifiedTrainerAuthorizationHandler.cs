using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using TrainingHub.Shared.Api.Identity;
using TrainingHub.Shared.Application.Queries;

namespace TrainingHub.Shared.Api.Authorization;

/// <summary>
/// Decides the <see cref="VerifiedTrainerPolicy"/>: the caller's account must have proven its
/// email address.
/// </summary>
/// <remarks>
/// Shared by both hosts, like the standing handler beside it and for the same reason. The flag
/// is read on every guarded request rather than taken from the token, and that is the point: the
/// token is minted at sign-in and there is no refresh token, so a verification proven while the
/// trainer is signed in would not reach a claim until they signed in again — and making them
/// sign out to use the address they just proved would turn the happy path into a support ticket
/// (ADR 0090).
/// </remarks>
public sealed class VerifiedTrainerAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    IAccountVerificationQuery accountVerificationQuery)
    : AuthorizationHandler<VerifiedTrainerRequirement>
{
    /// <summary>
    /// Grants the requirement unless the caller is a trainer whose address is unproven.
    /// </summary>
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        VerifiedTrainerRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        if (httpContext.User.FindFirst(TrainerClaims.TrainerId) is null)
        {
            // Nobody's trainer, so nobody this policy has anything to say about — the standing
            // handler's reasoning: TrainerPolicy is what refuses them, and failing here as well
            // would answer the same caller twice.
            context.Succeed(requirement);
            return;
        }

        var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            // A trainer claim without a user identifier is a token this product never mints;
            // leaving the requirement unmet refuses the request without deciding anything.
            return;
        }

        if (await accountVerificationQuery.IsVerifiedAsync(userId, httpContext.RequestAborted))
        {
            context.Succeed(requirement);
        }
    }
}
