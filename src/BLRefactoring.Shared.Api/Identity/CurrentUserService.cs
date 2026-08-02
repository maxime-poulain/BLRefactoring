using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace BLRefactoring.Shared.Api.Identity;

/// <summary>
/// Reads the current caller from the request's claims.
/// </summary>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    /// <summary>
    /// The identity account behind the request, read from the subject claim.
    /// </summary>
    public Guid UserId => httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value is
        { } userId
        ? Guid.Parse(userId)
        : throw new ApplicationException("Invalid user id");

    /// <summary>
    /// The trainer the caller is, read from the <c>trainer_id</c> claim.
    /// </summary>
    public Guid TrainerId => httpContextAccessor.HttpContext?.User.FindFirst("trainer_id")?.Value is { } trainerId
        ? Guid.Parse(trainerId)
        : throw new ApplicationException("Invalid trainer id");
}
