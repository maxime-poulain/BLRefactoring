using System.Transactions;
using BLRefactoring.DDD.Application.Services.TrainerServices;
using BLRefactoring.Shared.Application.Dtos.Trainer;
using BLRefactoring.Shared.Infrastructure.ThirdParty.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.DDD.Api.Controller;

/// <summary>
/// Controller for handling authentication-related operations such as user registration and login.
/// </summary>
public class AuthController(
    UserManager<IdentityUser<Guid>> userManager,
    ITokenService tokenService,
    ITrainerApplicationService trainerApplicationService) : ApiControllerBase
{
    /// <summary>
    /// Registers a new user with the provided username, email, and password.
    /// </summary>
    /// <param name="request">The registration request containing username, email, and password.</param>
    /// <returns>
    /// A 200 OK response if the registration is successful.
    /// A 400 Bad Request response with a list of identity errors if the registration fails.
    /// </returns>
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

        var creationResult = await trainerApplicationService.CreateAsync(
            new TrainerCreationRequest()
            {
                Email = user.Email, Firstname = request.Firstname, Lastname = request.Lastname, UserId = user.Id, Bio = "<>"
            }, cancellationToken);

        transactionScope.Complete();
        return creationResult.Match<ActionResult>(
            _ => Ok(),
            collection => BadRequest(collection.Select(x => new IdentityError()
            {
                Code = x.ErrorCode.Name, Description = x.ErrorMessage
            })));
    }

    /// <summary>
    /// Authenticates a user and generates a JWT token if the credentials are valid.
    /// </summary>
    /// <param name="request">The login request containing username and password.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A 200 OK response with the generated JWT token if authentication is successful.
    /// A 401 Unauthorized response with an error message if authentication fails.
    /// </returns>
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

/// <summary>
/// Represents a request to register a new user.
/// </summary>
public sealed class RegisterRequest
{
    /// <summary>
    /// The username of the new user.
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// The email address of the new user.
    /// </summary>
    public required string Email { get; init; }

    /// <summary>
    /// The password for the new user.
    /// </summary>
    public required string Password { get; init; }

    /// <summary>
    /// The confirmation of the password.
    /// </summary>
    public required string ConfirmPassword { get; init; }

    /// <summary>
    /// The first name of the new user.
    /// </summary>
    public required string Firstname { get; init; }

    /// <summary>
    /// The last name of the new user.
    /// </summary>
    public required string Lastname { get; init; }
}

/// <summary>
/// Represents a request to log in a user.
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// The username of the user attempting to log in.
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// The password of the user attempting to log in.
    /// </summary>
    public required string Password { get; init; }
}

/// <summary>
/// Represents the response returned after a successful login.
/// </summary>
public sealed class LoginResponse
{
    /// <summary>
    /// The JWT token generated for the authenticated user.
    /// </summary>
    public required string Token { get; init; }
}
