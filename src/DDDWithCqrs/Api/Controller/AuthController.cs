using BLRefactoring.DDDWithCqrs.Api.Mappings;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Api.Controllers;
using BLRefactoring.Shared.Api.Identity;
using Microsoft.AspNetCore.Identity;

namespace BLRefactoring.DDDWithCqrs.Api.Controller;

/// <summary>
/// Authentication endpoints of the CQRS stack. The registration and login flows
/// live in <see cref="AuthControllerBase"/>; this controller only supplies how
/// the trainer is created, through the command dispatcher.
/// </summary>
public class AuthController(
    UserManager<IdentityUser<Guid>> userManager,
    SignInManager<IdentityUser<Guid>> signInManager,
    ITokenService tokenService,
    ICommandDispatcher commandDispatcher)
    : AuthControllerBase(userManager, signInManager, tokenService)
{
    /// <inheritdoc />
    /// <remarks>
    /// The command reports success and nothing more, so the identifier comes from the command
    /// itself — it generates one when it is built, exactly as <c>CreateTrainingCommand</c> does for
    /// the identifier that endpoint publishes in <c>Location</c>.
    /// </remarks>
    protected override async Task<Result<Guid>> CreateTrainerAsync(
        RegisterRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var command = request.ToCommand(userId);

        var result = await commandDispatcher.DispatchAsync(command, cancellationToken);

        return result.Match<Result<Guid>>(
            () => Result<Guid>.Success(command.TrainerId),
            Result<Guid>.Failure);
    }
}
