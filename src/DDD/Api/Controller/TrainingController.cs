using BLRefactoring.Shared.Api.Authorization;
using BLRefactoring.Shared.Api.Controllers;
using BLRefactoring.Shared.Api.Http;
using BLRefactoring.DDD.Application.Services.TrainingServices;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.Common.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.DDD.Api.Controller;

/// <summary>
/// API controller for managing training resources.
/// Provides REST endpoints for creating, reading, and updating training records.
/// </summary>
/// <param name="trainingApplicationService">Application service for training operations.</param>
[Authorize]
public class TrainingController(ITrainingApplicationService trainingApplicationService) : ApiControllerBase
{
    /// <summary>
    /// Creates a new training.
    /// </summary>
    /// <param name="request">The training creation request containing training details.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 201 Created with the created training ID on success.
    /// 409 Conflict when a training with the same title already exists for the trainer.
    /// 400 Bad Request with validation errors on failure.
    /// </returns>
    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<ActionResult> CreateTrainingAsync(
        [FromBody] TrainingCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await trainingApplicationService.CreateAsync(request, cancellationToken);

        return result.Match<ActionResult>(
            (trainingDto) => CreatedAtAction("GetTrainingById", new { id = trainingDto.Id }, trainingDto.Id),
            errors => errors.Any(error => error.ErrorCode == ErrorCode.DuplicateTitle)
                ? Conflict(errors)
                : BadRequest(errors));
    }

    /// <summary>
    /// Retrieves a training by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the training.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 200 OK with the training details if found.
    /// 404 Not Found if the training does not exist.
    /// 400 Bad Request on validation errors.
    /// </returns>
    [Authorize]
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(TrainingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetTrainingByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await trainingApplicationService.GetByIdAsync(id, cancellationToken);

        // The ETag published here is what the caller must send back as If-Match
        // when they later edit this training.
        return result.Match<ActionResult>(
            trainingDto =>
            {
                this.SetETag(trainingDto.RowVersion);
                return Ok(trainingDto);
            },
            errors =>
            {
                return errors.Any(error => error.ErrorCode == ErrorCode.NotFound)
                    ? NotFound(errors)
                    : BadRequest(errors);
            });
    }

    /// <summary>
    /// Retrieves all available trainings.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>200 OK with a list of all trainings.</returns>
    [Authorize]
    [HttpGet("all")]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<TrainingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TrainingDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Ok(await trainingApplicationService.GetAllAsync(cancellationToken));
    }

    /// <summary>
    /// Updates an existing training.
    /// </summary>
    /// <param name="trainingId">The unique identifier of the training to update.</param>
    /// <param name="request">The training update request containing updated training details.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 200 OK if the update was successful.
    /// 403 Forbidden if the current user is not the training owner.
    /// 404 Not Found if the training does not exist.
    /// 409 Conflict when a training with the same title already exists for the trainer.
    /// 412 Precondition Failed when the training changed since the caller read it.
    /// 428 Precondition Required when the request carries no If-Match header.
    /// 400 Bad Request on validation errors.
    /// </returns>
    /// <remarks>
    /// The request must carry an <c>If-Match</c> holding the <c>ETag</c> returned
    /// when the training was read, so an edit based on a stale copy is rejected
    /// instead of silently overwriting someone else's changes.
    /// </remarks>
    [Authorize(Policy = TrainingOwnerPolicy.Name)]
    [HttpPut("{trainingId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(TrainingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status428PreconditionRequired)]
    public async Task<ActionResult> UpdateTrainingAsync(
        Guid trainingId,
        [FromBody] TrainingEditionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetExpectedVersion(out var expectedVersion))
        {
            return this.PreconditionRequired();
        }

        var result = await trainingApplicationService.EditAsync(
            trainingId, request, expectedVersion, cancellationToken);

        return result.Match<ActionResult>(
            training =>
            {
                this.SetETag(training.RowVersion);
                return Ok(training);
            },
            errors =>
            {
                if (errors.Any(error => error.ErrorCode == ErrorCode.NotFound))
                {
                    return NotFound(errors);
                }

                if (errors.Any(error => error.ErrorCode == ErrorCode.ConcurrencyConflict))
                {
                    return this.PreconditionFailed(errors);
                }

                return errors.Any(error => error.ErrorCode == ErrorCode.DuplicateTitle)
                    ? Conflict(errors)
                    : BadRequest(errors);
            });
    }

    /// <summary>
    /// Retrieves all trainings associated with a specific trainer.
    /// </summary>
    /// <param name="trainerId">The unique identifier of the trainer whose trainings are to be retrieved.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 200 OK with a list of trainings associated with the trainer.
    /// 400 Bad Request on validation errors.
    /// </returns>
    [Authorize]
    [HttpGet("by-trainer/{trainerId:guid}")]
    [ProducesResponseType(typeof(List<TrainingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByTrainerIdAsync(Guid trainerId, CancellationToken cancellationToken = default)
    {
        var result = await trainingApplicationService.GetByTrainerIdAsync(trainerId, cancellationToken);
        return result.Match<IActionResult>(Ok,BadRequest);
    }

    /// <summary>
    /// Retrieves all trainings that match the specified topic.
    /// </summary>
    /// <param name="topic">The topic name to filter trainings by.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>200 OK with a list of trainings matching the topic.</returns>
    [Authorize]
    [HttpGet("by-topic/{topic}")]
    [ProducesResponseType(typeof(List<TrainingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByTopicAsync(string topic, CancellationToken cancellationToken = default)
    {
        var trainings = await trainingApplicationService.GetByTopicAsync(topic, cancellationToken);
        return Ok(trainings);
    }

    /// <summary>
    /// Deletes a training by its unique identifier.
    /// </summary>
    /// <param name="trainingId">The unique identifier of the training to delete.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 204 No Content if the deletion was successful.
    /// 404 Not Found if the training does not exist.
    /// 400 Bad Request on validation errors.
    /// </returns>

    [Authorize(Policy = TrainingOwnerPolicy.Name)]
    [HttpDelete("{trainingId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteTrainingAsync(Guid trainingId, CancellationToken cancellationToken = default)
    {
        var result = await trainingApplicationService.DeleteAsync(trainingId, cancellationToken);
        return result.Match<IActionResult>(
            NoContent,
            errors =>
            {
                return errors.Any(error => error.ErrorCode == ErrorCode.NotFound)
                    ? NotFound(errors)
                    : BadRequest(errors);
            });
    }
}
