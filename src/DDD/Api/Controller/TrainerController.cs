using BLRefactoring.DDD.Api.Mappings;
using BLRefactoring.DDD.Application.Services.TrainerServices;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Api.Contracts.Errors;
using BLRefactoring.Shared.Api.Contracts.Mappings;
using BLRefactoring.Shared.Api.Contracts.Trainers;
using BLRefactoring.Shared.Api.Controllers;
using BLRefactoring.Shared.Api.Http;
using BLRefactoring.Shared.Common.Errors;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.DDD.Api.Controller;

/// <summary>
/// API controller for reading and editing trainer resources.
/// Trainers are only created through the registration flow, which creates the identity user and
/// its trainer atomically, and no endpoint deletes one: removing a trainer is an administrative
/// decision, not something a trainer performs on themselves.
/// </summary>
/// <remarks>
/// The action signatures speak only in API contracts. What the application service accepts and
/// returns is translated on either side, so the published API can change without the service
/// changing, and the reverse.
/// </remarks>
/// <param name="trainerApplicationService">Application service for trainer operations.</param>
/// <param name="currentUserService">Provides the identity of the caller.</param>
public class TrainerController(
    ITrainerApplicationService trainerApplicationService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    /// <summary>
    /// Retrieves the profile of the authenticated trainer.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 200 OK with the profile and the <c>ETag</c> to send back when editing it.
    /// 404 Not Found if the token refers to a trainer that no longer exists.
    /// </returns>
    /// <remarks>
    /// The counterpart of <c>PUT /Trainer/me</c>: since editing requires the version
    /// the caller read, they need a way to read their own profile without knowing
    /// their identifier.
    /// </remarks>
    [HttpGet("me")]
    [ProducesResponseType(typeof(TrainerResponseHttp), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainerResponseHttp>> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var result = await trainerApplicationService.GetByIdAsync(
            currentUserService.TrainerId, cancellationToken);

        return result.Match<ActionResult>(
            trainer =>
            {
                this.SetETag(trainer.RowVersion);
                return Ok(trainer.ToHttp());
            },
            errors =>
                errors.Any(error => error.ErrorCode == ErrorCode.NotFound)
                    ? NotFound()
                    : BadRequest(errors.ToHttp()));
    }

    /// <summary>
    /// Replaces the profile of the authenticated trainer.
    /// </summary>
    /// <param name="request">The new state of the profile.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 200 OK with the updated trainer.
    /// 400 Bad Request on validation errors.
    /// 404 Not Found if the token refers to a trainer that no longer exists.
    /// </returns>
    /// <remarks>
    /// The trainer being edited is the one carried by the token rather than a route
    /// parameter: a trainer only ever edits their own profile, so there is no
    /// ownership to check and no identifier to tamper with.
    /// Editing the contact email leaves the identity account untouched — it is not
    /// the credential used to sign in.
    /// The request must carry an <c>If-Match</c> holding the <c>ETag</c> returned
    /// when the profile was read, so an edit based on a stale copy is rejected
    /// instead of silently overwriting someone else's changes.
    /// </remarks>
    [HttpPut("me")]
    [ProducesResponseType(typeof(TrainerResponseHttp), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IEnumerable<ErrorResponseHttp>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(IEnumerable<ErrorResponseHttp>), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(IEnumerable<ErrorResponseHttp>), StatusCodes.Status428PreconditionRequired)]
    public async Task<ActionResult<TrainerResponseHttp>> EditCurrentAsync(
        [FromBody] EditTrainerRequestHttp request,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetExpectedVersion(out var expectedVersion))
        {
            return this.PreconditionRequired();
        }

        var result = await trainerApplicationService.EditAsync(
            currentUserService.TrainerId,
            request.ToApplicationRequest(),
            expectedVersion,
            cancellationToken);

        return result.Match<ActionResult>(
            trainer =>
            {
                this.SetETag(trainer.RowVersion);
                return Ok(trainer.ToHttp());
            },
            errors =>
            {
                if (errors.Any(error => error.ErrorCode == ErrorCode.NotFound))
                {
                    return NotFound();
                }

                return errors.Any(error => error.ErrorCode == ErrorCode.ConcurrencyConflict)
                    ? this.PreconditionFailed(errors.ToHttp())
                    : BadRequest(errors.ToHttp());
            });
    }

    /// <summary>
    /// Retrieves a trainer by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the trainer.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 200 OK with the trainer details if found.
    /// 404 Not Found if the trainer does not exist.
    /// 400 Bad Request on validation errors.
    /// </returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(IEnumerable<ErrorResponseHttp>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(TrainerResponseHttp), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrainerResponseHttp>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await trainerApplicationService.GetByIdAsync(id, cancellationToken);

        // The ETag published here is what the caller must send back as If-Match
        // when they later edit this profile.
        return result.Match<ActionResult>(
            trainer =>
            {
                this.SetETag(trainer.RowVersion);
                return Ok(trainer.ToHttp());
            },
            errors =>
                errors.Any(error => error.ErrorCode == ErrorCode.NotFound)
                    ? NotFound()
                    : BadRequest(errors.ToHttp()));
    }

    /// <summary>
    /// Retrieves all available trainers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>200 OK with a list of all trainers.</returns>
    [HttpGet("all")]
    [ProducesResponseType(typeof(IEnumerable<ErrorResponseHttp>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<TrainerResponseHttp>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TrainerResponseHttp>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var trainers = await trainerApplicationService.GetAllAsync(cancellationToken);
        return Ok(trainers.ToHttp());
    }
}
