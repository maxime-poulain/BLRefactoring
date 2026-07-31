using BLRefactoring.Shared.Infrastructure.Http;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Edit;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetAll;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetById;
using BLRefactoring.Shared;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.CQS;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.DDDWithCqrs.Api.Controller;

/// <summary>
/// Trainers are only created through the registration flow, which creates
/// the identity user and its trainer atomically.
/// </summary>
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
    [ProducesResponseType(typeof(TrainerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainerDto>> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var trainer = await queryDispatcher.DispatchAsync(
            new GetTrainerByIdQuery(currentUserService.TrainerId), cancellationToken);

        if (trainer is null)
        {
            return NotFound();
        }

        this.SetETag(trainer.RowVersion);
        return Ok(trainer);
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
    [ProducesResponseType(typeof(TrainerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status428PreconditionRequired)]
    public async Task<ActionResult<TrainerDto>> EditCurrentAsync(
        [FromBody] EditTrainerCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetExpectedVersion(out var expectedVersion))
        {
            return this.PreconditionRequired();
        }

        command.TrainerId = currentUserService.TrainerId;
        command.ExpectedVersion = expectedVersion;

        var result = await commandDispatcher.DispatchAsync(command, cancellationToken);

        return await result.MatchAsync<ActionResult>(
            onSuccess: async () =>
            {
                var trainer = await queryDispatcher.DispatchAsync(
                    new GetTrainerByIdQuery(command.TrainerId), cancellationToken);

                if (trainer is null)
                {
                    return NotFound();
                }

                this.SetETag(trainer.RowVersion);
                return Ok(trainer);
            },
            onFailure: errors => ValueTask.FromResult<ActionResult>(
                errors.Any(error => error.ErrorCode == ErrorCode.NotFound) ? NotFound()
                : errors.Any(error => error.ErrorCode == ErrorCode.ConcurrencyConflict) ? this.PreconditionFailed(errors)
                : BadRequest(errors)));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(TrainerDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrainerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var trainer = await queryDispatcher.DispatchAsync(new GetTrainerByIdQuery(id), cancellationToken);
        if (trainer is null)
        {
            return NotFound();
        }

        // The ETag published here is what the caller must send back as If-Match
        // when they later edit this profile.
        this.SetETag(trainer.RowVersion);
        return Ok(trainer);
    }

    [HttpGet("all")]
    [ProducesResponseType(typeof(List<TrainerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<TrainerDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Ok(await queryDispatcher.DispatchAsync(new GetAllTrainersQuery(), cancellationToken));
    }
}
