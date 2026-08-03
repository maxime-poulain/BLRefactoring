using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace TrainingHub.Shared.Api.Identity;

/// <summary>
/// Reads the current caller from the request's claims.
/// </summary>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    /// <summary>
    /// The identity account behind the request, read from the subject claim.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The request carries no subject claim. Every route that reaches this is behind
    /// authentication, so the absence of the claim is a configuration fault rather than a caller
    /// error, and it is raised rather than returned.
    /// </exception>
    public Guid UserId =>
        httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value is { } userId
            ? Guid.Parse(userId)
            : throw new InvalidOperationException(
                "The request carries no subject claim, so there is no signed-in user to read.");

    /// <summary>
    /// The trainer the caller is, read from the <c>trainer_id</c> claim.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The request carries no <c>trainer_id</c> claim.
    /// </exception>
    public Guid TrainerId =>
        httpContextAccessor.HttpContext?.User.FindFirst("trainer_id")?.Value is { } trainerId
            ? Guid.Parse(trainerId)
            : throw new InvalidOperationException(
                "The request carries no trainer_id claim, so there is no trainer to act as.");
}
