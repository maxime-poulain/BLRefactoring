using System.Transactions;
using TrainingHub.Shared.Api.Authorization;
using TrainingHub.Shared.Api.Contracts.Auth;
using TrainingHub.Shared.Api.Http;
using TrainingHub.Shared.Api.Identity;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Common.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace TrainingHub.Shared.Api.Controllers;

/// <summary>
/// Shared implementation of the account endpoints (registration, login, password recovery and
/// erasure). The whole flow is identical in both stacks except for how the trainer itself is
/// created or erased, which each host supplies through <see cref="CreateTrainerAsync"/> and
/// <see cref="EraseTrainerAsync"/>.
/// </summary>
[ApiController]
[Route("[controller]")]
public abstract class AuthControllerBase(
    UserManager<IdentityUser<Guid>> userManager,
    SignInManager<IdentityUser<Guid>> signInManager,
    ITokenService tokenService,
    IPasswordRecoveryService passwordRecovery,
    IIntegrationEventPublisher integrationEventPublisher) : ControllerBase
{
    /// <summary>
    /// Creates the trainer associated with the freshly registered identity user, and reports
    /// its identifier. Runs inside the registration transaction: a failure result rolls back
    /// the identity user as well.
    /// </summary>
    /// <remarks>
    /// The identifier is what makes the registration a <c>201</c> rather than a <c>204</c>: it is
    /// the address published in <c>Location</c>. Each stack knows it by a different route — the
    /// layered one reads it off the created aggregate, the CQRS one generates it into the command
    /// before dispatching — so the base class asks for it rather than deriving it.
    /// </remarks>
    /// <param name="request">The registration request carrying the trainer's data.</param>
    /// <param name="userId">The identifier of the identity user that was just created.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    [NonAction]
    protected abstract Task<Result<Guid>> CreateTrainerAsync(
        RegisterHttpRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Erases the calling trainer — the aggregate, and through the cascade its trainings — and
    /// saves. Runs inside the erasure transaction: the save also flushes the account fact the
    /// base staged, and a failure result rolls everything back (ADR 0085).
    /// </summary>
    /// <remarks>
    /// Registration's seam, mirrored: the base owns the transaction and the Identity half, each
    /// host supplies the trainer half in its own style. Nothing is reported back — the CQRS
    /// command answers a bare <c>Result</c>, and there is deliberately nothing to read after an
    /// erasure.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    [NonAction]
    protected abstract Task<Result> EraseTrainerAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a new user with the provided username, email, and password.
    /// </summary>
    /// <param name="request">The registration request containing username, email, and password.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A 201 Created response carrying the new trainer's identifier, and a <c>Location</c>
    /// pointing at it.
    /// A 409 Conflict response when the username or the email address is already taken.
    /// A 400 Bad Request response on any other failure.
    /// Both failures are problem documents keyed by field, like every other validation failure
    /// this API publishes.
    /// </returns>
    /// <remarks>
    /// A registration creates something, so it answers <c>201</c> and says where. This used to be a
    /// <c>204</c>, argued from the account having no address of its own — which is true of the
    /// identity user and false of what the caller actually gets: registration creates a *trainer*,
    /// and a trainer is addressable at <c>/Trainer/me</c>. That is the <c>Location</c>, and the
    /// identifier is repeated in the body so a caller need not parse a URL to get it — the shape
    /// <c>POST /Training</c> already publishes.
    /// <para>
    /// The address requires a token the registration response does not hand out. That is not a
    /// reason to withhold it: <c>Location</c> identifies the resource created, it does not promise
    /// the caller is authorized to read it, and the caller is one <c>POST /Auth/login</c> away.
    /// <c>/Trainer/me</c> was the wrong candidate to compare it against — it is an alias for
    /// whoever is calling, not the address of the thing this request created.
    /// </para>
    /// <para>
    /// A username or email already in use is a <c>409</c>, not a <c>400</c>: the request is
    /// well-formed and the caller can act on it — by choosing another — which is exactly the
    /// distinction this API already draws for a duplicate training title. Everything else Identity
    /// rejects (password policy, malformed email, illegal characters in a username) stays a
    /// <c>400</c>.
    /// </para>
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterHttpRequest request, CancellationToken cancellationToken = default)
    {
        using var transactionScope = new TransactionScope(TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled);

        if (request.Password != request.ConfirmPassword)
        {
            return ValidationFailure(
                StatusCodes.Status400BadRequest,
                new Dictionary<string, string[]>
                {
                    [nameof(RegisterHttpRequest.ConfirmPassword)] =
                        ["The password and confirmation password do not match."]
                });
        }

        var user = new IdentityUser<Guid> { UserName = request.Username, Email = request.Email, };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return IdentityRejection(result.Errors);
        }

        var creationResult = await CreateTrainerAsync(request, user.Id, cancellationToken);

        // The transaction is only completed when the whole registration succeeded.
        // If the trainer creation fails, disposing the scope without Complete()
        // rolls back the identity user as well, so no orphan account survives.
        return creationResult.Match(
            trainerId =>
            {
                transactionScope.Complete();

                // The action lives on the other controller of whichever host is running, which is
                // why it is named by string. Both hosts publish it at Trainer/me, and an
                // integration test on each asserts this header — so a rename that would leave
                // Location pointing nowhere fails the suite rather than the caller.
                //
                // It pointed at Trainer/{id} until that read was withdrawn for serving any
                // trainer's profile to any caller. `me` is relative to the token, which ADR 0011
                // rejected as a Location on the grounds that two callers get the same string; the
                // objection stands and no longer decides anything, because it is the only address
                // this resource has. The identifier a caller needs is in the body regardless.
                return CreatedAtAction(
                    actionName: "GetCurrent",
                    controllerName: "Trainer",
                    routeValues: null,
                    value: trainerId);
            },
            // Trainer creation fails on the domain's own rules, which carry this API's error codes,
            // so they leave through the one place a business failure becomes a body.
            errors => this.Problem(StatusCodes.Status400BadRequest, errors));
    }

    /// <summary>
    /// Answers a set of Identity failures at the status code they deserve, in the shape every
    /// other validation failure in this API uses.
    /// </summary>
    /// <remarks>
    /// A taken username or email is the one failure here that is not about the request being
    /// wrong — it is about the world having changed — so it gets the status this API already
    /// reserves for that, 409. Identity reports every rule it broke at once, so a request that is
    /// both a duplicate and an illegal password answers 409: the conflict is the part the caller
    /// cannot discover by re-reading their own request, and the whole list travels in the body
    /// either way.
    /// <para>
    /// The body is a <see cref="ValidationProblemDetails"/> keyed by the field at fault, which is
    /// what <c>[ApiController]</c> produces for a data annotation and what
    /// <c>ValidationExceptionHandler</c> produces for FluentValidation. It used to be a bare
    /// <c>IdentityError</c> array — the shape ADR 0004 removed from forty other places, surviving
    /// here because these two endpoints were written before that decision and never revisited.
    /// Identity's codes are names with no numeric value, so mapping them onto a field is the
    /// honest translation; inventing numbers to fit <c>domainErrors</c> would not be.
    /// </para>
    /// </remarks>
    private ActionResult IdentityRejection(IEnumerable<IdentityError> errors)
    {
        var rejections = errors.ToList();

        var isTaken = rejections.Any(error =>
            error.Code is nameof(IdentityErrorDescriber.DuplicateUserName)
                       or nameof(IdentityErrorDescriber.DuplicateEmail));

        var failures = rejections
            .GroupBy(FieldAtFault)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());

        return ValidationFailure(
            isTaken ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest,
            failures);
    }

    /// <summary>
    /// Which field of the registration request an Identity failure is about.
    /// </summary>
    /// <remarks>
    /// The empty key is the convention <c>ModelState</c> uses for a failure that belongs to the
    /// request as a whole rather than to one of its fields, so a code this method does not know
    /// still arrives somewhere a client can render.
    /// </remarks>
    private static string FieldAtFault(IdentityError error) => error.Code switch
    {
        nameof(IdentityErrorDescriber.DuplicateUserName) or
            nameof(IdentityErrorDescriber.InvalidUserName) => nameof(RegisterHttpRequest.Username),
        nameof(IdentityErrorDescriber.DuplicateEmail) or
            nameof(IdentityErrorDescriber.InvalidEmail) => nameof(RegisterHttpRequest.Email),
        _ when error.Code.StartsWith("Password", StringComparison.Ordinal) =>
            nameof(RegisterHttpRequest.Password),
        _ => string.Empty
    };

    /// <summary>
    /// Builds a problem document keyed by field, at the given status.
    /// </summary>
    /// <remarks>
    /// <see cref="ObjectResult"/> with the media type stated rather than
    /// <c>BadRequest(object)</c>, so the response carries
    /// <c>application/problem+json</c> like every other error this API sends.
    /// </remarks>
    private ActionResult ValidationFailure(int statusCode, IDictionary<string, string[]> failures)
    {
        var problem = new ValidationProblemDetails(failures)
        {
            Status = statusCode,
            Instance = Request.Path
        };

        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
    }

    /// <summary>
    /// Authenticates a user and generates a JWT token if the credentials are valid.
    /// </summary>
    /// <param name="request">The login request containing username and password.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A 200 OK response with the generated JWT token if authentication is successful.
    /// A 401 Unauthorized problem document if authentication fails.
    /// </returns>
    /// <remarks>
    /// The 401 used to be <c>Unauthorized("Invalid username or password.")</c>, which serializes to
    /// a bare JSON string — quotes included, no status, no member to read. ADR 0004 lists that
    /// exact shape as the worst of the four it removed, and then left it standing on the first
    /// call any client makes. The sentence is unchanged; only its wrapping is.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginHttpResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginHttpRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByNameAsync(request.Username);
        if (user == null)
        {
            return InvalidCredentials();
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
            return InvalidCredentials();
        }

        var roles = await userManager.GetRolesAsync(user);
        var token = await tokenService.GenerateTokenAsync(user, roles, cancellationToken);
        return Ok(new LoginHttpResponse() { Token = token });
    }

    /// <summary>
    /// Takes a password-reset request for an address, and answers the same way whether or not an
    /// account listens there.
    /// </summary>
    /// <param name="request">The request carrying the address to recover.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A 202 Accepted response with no body, always: the request was recorded, and whatever
    /// happens next happens by email.
    /// A 400 Bad Request response only for a body the contract itself refuses.
    /// </returns>
    /// <remarks>
    /// The <c>202</c> is literal — nothing is done yet. One outbox row is committed, and the
    /// delivery worker later looks the address up, mints the credential and sends the link
    /// (ADR 0084). Because the lookup is not on this path, a known and an unknown address cost
    /// the same work and answer the same status in the same time: this endpoint holds the
    /// discipline sign-in already holds, rather than joining registration on the other side of
    /// it.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordHttpRequest request,
        CancellationToken cancellationToken = default)
    {
        await passwordRecovery.RequestAsync(request.Email, cancellationToken);

        return Accepted();
    }

    /// <summary>
    /// Redeems an emailed reset link against a new password.
    /// </summary>
    /// <param name="request">The address, the token the link carried, and the new password twice.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A 204 No Content response when the password was changed — the link is spent.
    /// A 400 Bad Request response, keyed by field: one fixed sentence on <c>Token</c> for every
    /// way a link can be dead, or Identity's own words on <c>Password</c> when the link was fine
    /// and the password was not.
    /// </returns>
    /// <remarks>
    /// The failures are deliberately unequal in detail. A dead link answers one sentence whatever
    /// killed it — unknown address, wrong token, expired, spent, superseded, lost race — because
    /// naming the difference would name which addresses have accounts (ADR 0084). A refused
    /// password is explained in full, because only the caller holding a live link — the account's
    /// owner — can ever reach that refusal, and their link survives it.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordHttpRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Password != request.ConfirmPassword)
        {
            return ValidationFailure(
                StatusCodes.Status400BadRequest,
                new Dictionary<string, string[]>
                {
                    [nameof(ResetPasswordHttpRequest.ConfirmPassword)] =
                        ["The password and confirmation password do not match."]
                });
        }

        var outcome = await passwordRecovery.ResetAsync(
            request.Email, request.Token, request.Password, cancellationToken);

        return outcome switch
        {
            PasswordResetOutcome.Succeeded => NoContent(),
            PasswordResetOutcome.PasswordRefused refused => IdentityRejection(refused.Errors),
            _ => RefusedLink()
        };
    }

    /// <summary>
    /// Erases the calling trainer's account: the Identity account, the trainer, their trainings —
    /// everything, in one transaction, against the caller's own password.
    /// </summary>
    /// <param name="request">The request carrying the caller's current password.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A 204 No Content response when the account is gone — there is nothing left to address.
    /// A 400 Bad Request response keyed on <c>Password</c> when the password is wrong, or
    /// carrying the domain's own refusal when the trainer could not be erased.
    /// A 401 Unauthorized response when the caller presents no live session, or one whose
    /// account no longer exists.
    /// </returns>
    /// <remarks>
    /// Guarded by the trainer claim rather than the active standing, deliberately: a suspended
    /// trainer keeps this one write, because the right to leave outlives the sanction
    /// (ADR 0085, amending ADR 0053). An administrator's token carries no trainer claim and is
    /// refused here — that account was provisioned by hand and leaves by hand (ADR 0051).
    /// <para>
    /// The password is checked before anything opens, because a session is not proof of intent:
    /// an access token outlives an unlocked device by up to sixty minutes, and this is the one
    /// action nobody can undo. Wrong answers are field-keyed rather than generic — the caller is
    /// already authenticated as this account's holder, so there is nothing to enumerate.
    /// </para>
    /// <para>
    /// The transaction is registration's mirror (ADR 0040): the account fact is staged first —
    /// carrying the address and username the farewell will need, because at delivery time this
    /// row no longer exists — then the host-supplied trainer half stages, cascades and saves,
    /// flushing the fact with it; then the account row goes, and the reset credential follows it
    /// by foreign key (ADR 0084). Either everything is gone or nothing is.
    /// </para>
    /// </remarks>
    [Authorize(Policy = TrainerPolicy.Name)]
    [HttpPost("erase-account")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EraseAccount(
        [FromBody] EraseAccountHttpRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            // A live token over an account that no longer exists — erased from another session,
            // most likely. The session is over, and a body would have nothing true to say.
            return Unauthorized();
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            return ValidationFailure(
                StatusCodes.Status400BadRequest,
                new Dictionary<string, string[]>
                {
                    [nameof(EraseAccountHttpRequest.Password)] = ["The password is incorrect."]
                });
        }

        using var transactionScope = new TransactionScope(TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled);

        // Staged, not sent: the row rides the save the trainer half is about to make, and
        // evaporates with the scope if anything below refuses (ADR 0002).
        await integrationEventPublisher.PublishAsync(
            new AccountErasedIntegrationEvent(user.Id, user.Email!, user.UserName!),
            cancellationToken);

        var erasure = await EraseTrainerAsync(cancellationToken);

        return await erasure.MatchAsync<IActionResult>(
            async () =>
            {
                var deletion = await userManager.DeleteAsync(user);

                if (!deletion.Succeeded)
                {
                    // Disposing without Complete() puts the trainer, the trainings and the fact
                    // back; the account that refused to go is exactly as it was.
                    return IdentityRejection(deletion.Errors);
                }

                transactionScope.Complete();

                return NoContent();
            },
            errors => ValueTask.FromResult<IActionResult>(this.Problem(StatusCodes.Status400BadRequest, errors)));
    }

    /// <summary>
    /// The one answer a dead reset link gets, whatever killed it.
    /// </summary>
    /// <remarks>
    /// <see cref="InvalidCredentials"/>'s discipline, one flow over: a single method for every
    /// call site, so an expired link, a spent one and one for an address that never had an
    /// account cannot drift into distinguishable sentences.
    /// </remarks>
    private ActionResult RefusedLink() =>
        ValidationFailure(
            StatusCodes.Status400BadRequest,
            new Dictionary<string, string[]>
            {
                [nameof(ResetPasswordHttpRequest.Token)] =
                    ["This password reset link is invalid or has expired. Request a new one and use the most recent email."]
            });

    /// <summary>
    /// The one answer sign-in gives to every way of failing.
    /// </summary>
    /// <remarks>
    /// One method for both call sites so they cannot drift apart: an unknown username and a wrong
    /// password must be indistinguishable, and a locked-out account must look like both, or the
    /// endpoint becomes an oracle for which accounts exist. No <c>domainErrors</c> either — a code
    /// naming which half failed would give away exactly what the shared sentence withholds.
    /// </remarks>
    private ActionResult InvalidCredentials()
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = "Invalid username or password.",
            Instance = Request.Path
        };

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status401Unauthorized,
            ContentTypes = { "application/problem+json" }
        };
    }
}
