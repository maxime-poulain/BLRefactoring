using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create;
using BLRefactoring.Shared.Common.Results;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Infrastructure.ThirdParty.Identity;
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
        CancellationToken cancellationToken)
    {
        var command = new CreateTrainerCommand
        {
            Firstname = request.Firstname,
            Lastname = request.Lastname,
            // The contact email starts out as the account email; the trainer can
            // make it diverge later from their profile.
            ContactEmail = request.Email,
            UserId = userId
        };

        return await commandDispatcher.DispatchAsync(command, cancellationToken);
    }
}
