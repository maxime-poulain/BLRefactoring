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
    protected override async Task<Result> CreateTrainerAsync(
        RegisterRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await commandDispatcher.DispatchAsync(
            request.ToCommand(userId), cancellationToken);
    }
}
