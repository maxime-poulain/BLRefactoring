using BLRefactoring.DDD.Application.Services.TrainerServices;
using BLRefactoring.Shared.Application.Dtos.Trainer;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.Infrastructure.ThirdParty.Identity;
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
        CancellationToken cancellationToken)
    {
        var creationResult = await trainerApplicationService.CreateAsync(
            new TrainerCreationRequest
            {
                Email = request.Email,
                Firstname = request.Firstname,
                Lastname = request.Lastname,
                UserId = userId
            }, cancellationToken);

        return creationResult.Match(_ => Result.Success(), Result.Failure);
    }
}
