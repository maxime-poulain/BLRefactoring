using System.Transactions;
using BLRefactoring.Shared.Api.Identity;
using BLRefactoring.Shared.Common.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.Shared.Api.Controllers;

/// <summary>
/// Shared implementation of the authentication endpoints (registration and login).
/// The whole flow is identical in both stacks except for how the trainer itself is
/// created, which each host supplies through <see cref="CreateTrainerAsync"/>.
/// </summary>
[ApiController]
[Route("[controller]")]
public abstract class AuthControllerBase(
    UserManager<IdentityUser<Guid>> userManager,
    SignInManager<IdentityUser<Guid>> signInManager,
    ITokenService tokenService) : ControllerBase
{
    /// <summary>
    /// Creates the trainer associated with the freshly registered identity user.
    /// Runs inside the registration transaction: a failure result rolls back the
    /// identity user as well.
    /// </summary>
    /// <param name="request">The registration request carrying the trainer's data.</param>
    /// <param name="userId">The identifier of the identity user that was just created.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    protected abstract Task<Result> CreateTrainerAsync(
        RegisterRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a new user with the provided username, email, and password.
    /// </summary>
    /// <param name="request">The registration request containing username, email, and password.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
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

        var creationResult = await CreateTrainerAsync(request, user.Id, cancellationToken);

        // The transaction is only completed when the whole registration succeeded.
        // If the trainer creation fails, disposing the scope without Complete()
        // rolls back the identity user as well, so no orphan account survives.
        return creationResult.Match<ActionResult>(
            () =>
            {
                transactionScope.Complete();
                return Ok();
            },
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

        // CheckPasswordSignInAsync enforces the lockout policy configured on
        // Identity: it increments the failed-access count, locks the account
        // at the threshold, rejects a locked-out account and resets the count
        // on success. The response stays identical for a wrong password and a
        // locked-out account so callers get no oracle about account state.
        var signInResult = await signInManager.CheckPasswordSignInAsync(
            user, request.Password, lockoutOnFailure: true);
        if (!signInResult.Succeeded)
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
public sealed class LoginRequest
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
