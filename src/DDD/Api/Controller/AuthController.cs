using BLRefactoring.DDD.Api.Mappings;
using BLRefactoring.DDD.Application.Services.TrainerServices;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.Api.Controllers;
using BLRefactoring.Shared.Api.Identity;
using Microsoft.AspNetCore.Identity;

namespace BLRefactoring.DDD.Api.Controller;

/// <summary>
/// Authentication endpoints of the DDD stack. The registration and login flows
/// live in <see cref="AuthControllerBase"/>; this controller only supplies how
/// the trainer is created, through the trainer application service.
/// </summary>
public class AuthController(
    UserManager<IdentityUser<Guid>> userManager,
    SignInManager<IdentityUser<Guid>> signInManager,
    ITokenService tokenService,
    ITrainerApplicationService trainerApplicationService)
    : AuthControllerBase(userManager, signInManager, tokenService)
{
    /// <inheritdoc />
    protected override async Task<Result> CreateTrainerAsync(
        RegisterRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var creationResult = await trainerApplicationService.CreateAsync(
            request.ToApplicationRequest(userId), cancellationToken);

        return creationResult.Match(_ => Result.Success(), Result.Failure);
    }
}
