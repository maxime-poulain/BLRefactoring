using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace BLRefactoring.Shared.Infrastructure.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid UserId => httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value is
        { } userId
        ? Guid.Parse(userId)
        : throw new ApplicationException("Invalid user id");

    public Guid TrainerId => httpContextAccessor.HttpContext?.User.FindFirst("trainer_id")?.Value is { } trainerId
        ? Guid.Parse(trainerId)
        : throw new ApplicationException("Invalid trainer id");
}
