using BLRefactoring.DDDWithCqrs.Api.Contracts;
using BLRefactoring.DDDWithCqrs.Api.Mappings;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Api.Contracts.Errors;
using BLRefactoring.Shared.Api.Contracts.Mappings;
using BLRefactoring.Shared.Api.Contracts.Trainers;
using BLRefactoring.Shared.Api.Controllers;
using BLRefactoring.Shared.Api.Http;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.CQS;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.DDDWithCqrs.Api.Controller;

/// <summary>
/// Trainers are only created through the registration flow, which creates
/// the identity user and its trainer atomically.
/// </summary>
/// <remarks>
/// No command or query appears in this file. They are built by
/// <see cref="HttpToApplicationMappings"/> from the API's own contracts, which is what lets the
/// published API and the CQRS messages change independently — and what removed the
/// <c>[JsonIgnore]</c> attributes the commands used to need to be bound from a request body.
/// </remarks>
public class TrainerController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    /// <summary>
    /// Retrieves the profile of the authenticated trainer.
    /// </summary>
    /// <remarks>
    /// The counterpart of <c>PUT /Trainer/me</c>: since editing requires the version
    /// the caller read, they need a way to read their own profile without knowing
    /// their identifier.
    /// </remarks>
    [HttpGet("me")]
    [ProducesResponseType(typeof(TrainerResponseHttp), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainerResponseHttp>> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var trainer = await queryDispatcher.DispatchAsync(
            HttpToApplicationMappings.ToGetTrainerByIdQuery(currentUserService.TrainerId), cancellationToken);

        if (trainer is null)
        {
            return NotFound();
        }

        this.SetETag(trainer.RowVersion);
        return Ok(trainer.ToHttp());
    }

    /// <summary>
    /// Replaces the profile of the authenticated trainer.
    /// </summary>
    /// <remarks>
    /// The trainer being edited is the one carried by the token rather than a route
    /// parameter: a trainer only ever edits their own profile, so there is no
    /// ownership to check and no identifier to tamper with.
    /// Editing the contact email leaves the identity account untouched — it is not
    /// the credential used to sign in.
    /// The command answers whether the write succeeded; the updated representation
    /// is then read back through the query side rather than returned by the command.
    /// </remarks>
    [HttpPut("me")]
    [ProducesResponseType(typeof(TrainerResponseHttp), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IEnumerable<ErrorResponseHttp>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
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

        var trainerId = currentUserService.TrainerId;
        var result = await commandDispatcher.DispatchAsync(
            request.ToCommand(trainerId, expectedVersion), cancellationToken);

        return await result.MatchAsync<ActionResult>(
            onSuccess: async () =>
            {
                var trainer = await queryDispatcher.DispatchAsync(
                    HttpToApplicationMappings.ToGetTrainerByIdQuery(trainerId), cancellationToken);

                if (trainer is null)
                {
                    return NotFound();
                }

                this.SetETag(trainer.RowVersion);
                return Ok(trainer.ToHttp());
            },
            onFailure: errors => ValueTask.FromResult<ActionResult>(
                errors.Any(error => error.ErrorCode == ErrorCode.NotFound) ? NotFound()
                : errors.Any(error => error.ErrorCode == ErrorCode.ConcurrencyConflict) ? this.PreconditionFailed(errors.ToHttp())
                : BadRequest(errors.ToHttp())));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(IEnumerable<ErrorResponseHttp>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(TrainerResponseHttp), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrainerResponseHttp>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var trainer = await queryDispatcher.DispatchAsync(
            HttpToApplicationMappings.ToGetTrainerByIdQuery(id), cancellationToken);

        if (trainer is null)
        {
            return NotFound();
        }

        // The ETag published here is what the caller must send back as If-Match
        // when they later edit this profile.
        this.SetETag(trainer.RowVersion);
        return Ok(trainer.ToHttp());
    }

    /// <summary>
    /// Returns one page of trainers, newest first.
    /// </summary>
    [HttpGet("all")]
    [ProducesResponseType(typeof(PagedResponseHttp<TrainerResponseHttp>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IEnumerable<ErrorResponseHttp>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponseHttp<TrainerResponseHttp>>> GetAllAsync(
        [FromQuery] PaginationRequestHttp pagination,
        CancellationToken cancellationToken = default)
    {
        var page = await queryDispatcher.DispatchAsync(
            pagination.ToGetAllTrainersQuery(), cancellationToken);

        return Ok(page.ToHttp(trainers => trainers.ToHttp()));
    }
}
