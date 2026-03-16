using System.Transactions;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Infrastructure.ThirdParty.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.DDDWithCqrs.Api.Controller;

public class AuthController(
    UserManager<IdentityUser<Guid>> userManager,
    ITokenService tokenService,
    ICommandDispatcher commandDispatcher) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(void), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IEnumerable<IdentityError>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken = default)
    {
        using var transactionScope = new TransactionScope(TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled);

        if (request.Password != request.ConfirmPassword)
        {
            return BadRequest(new List<IdentityError>
            {
                new IdentityError
                {
                    Code = "PasswordMismatch",
                    Description = "The password and confirmation password do not match."
                }
            });
        }

        var user = new IdentityUser<Guid> { UserName = request.Username, Email = request.Email, };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        var command = new CreateTrainerCommand
        {
            Firstname = request.Firstname,
            Lastname = request.Lastname,
            Email = request.Email,
            UserId = user.Id
        };

        var creationResult = await commandDispatcher.DispatchAsync(command, cancellationToken);

        transactionScope.Complete();
        return creationResult.Match<ActionResult>(
            () => Ok(),
            collection => BadRequest(collection.Select(x => new IdentityError()
            {
                Code = x.ErrorCode.Name, Description = x.ErrorMessage
            })));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<string>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByNameAsync(request.Username);
        if (user == null)
        {
            return Unauthorized("Invalid username or password.");
        }

        var isPasswordValid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!isPasswordValid)
        {
            return Unauthorized("Invalid username or password.");
        }

        var roles = await userManager.GetRolesAsync(user);
        var token = await tokenService.GenerateTokenAsync(user, roles, cancellationToken);
        return Ok(new LoginResponse() { Token = token });
    }
}

public sealed class RegisterRequest
{
    public required string Username { get; init; }
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required string ConfirmPassword { get; init; }
    public required string Firstname { get; init; }
    public required string Lastname { get; init; }
}

public class LoginRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}

public sealed class LoginResponse
{
    public required string Token { get; init; }
}
