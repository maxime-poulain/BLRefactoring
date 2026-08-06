using TrainingHub.DDD.Api.Mappings;
using TrainingHub.DDD.Application.Services.TrainerServices;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.Api.Contracts.Auth;
using TrainingHub.Shared.Api.Controllers;
using TrainingHub.Shared.Api.Identity;
using Microsoft.AspNetCore.Identity;

namespace TrainingHub.DDD.Api.Controller;

/// <summary>
/// Authentication endpoints of the DDD stack. The registration and login flows
/// live in <see cref="AuthControllerBase"/>; this controller only supplies how
/// the trainer is created, through the trainer application service.
/// </summary>
public sealed class AuthController(
    UserManager<IdentityUser<Guid>> userManager,
    SignInManager<IdentityUser<Guid>> signInManager,
    ITokenService tokenService,
    ITrainerApplicationService trainerApplicationService)
    : AuthControllerBase(userManager, signInManager, tokenService)
{
    /// <inheritdoc />
    /// <remarks>
    /// The application service answers with the trainer it created, so its identifier is read off
    /// that read model rather than generated here.
    /// </remarks>
    protected override async Task<Result<Guid>> CreateTrainerAsync(
        RegisterRequestHttp request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var creationResult = await trainerApplicationService.CreateAsync(
            request.ToApplicationRequest(userId), cancellationToken);

        return creationResult.Match<Result<Guid>>(
            trainer => Result<Guid>.Success(trainer.Id),
            Result<Guid>.Failure);
    }
}
