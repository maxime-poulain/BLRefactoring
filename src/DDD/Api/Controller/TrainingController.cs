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
    /// <returns>
    /// 201 Created with the created training ID on success.
    /// 400 Bad Request with validation errors on failure.
    /// </returns>
    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<ActionResult> CreateTrainingAsync([FromBody] TrainingCreationRequest request)
    {
        var result = await trainingApplicationService.CreateAsync(request);

        return result.Match<ActionResult>(
            (trainingDto) => CreatedAtAction("GetTrainingById", new { id = trainingDto.Id }, trainingDto.Id),
            BadRequest);
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
    public async Task<ActionResult> GetTrainingByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await trainingApplicationService.GetByIdAsync(id, cancellationToken);

        return result.Match<ActionResult>(trainingDto => Ok(trainingDto),
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
    public async Task<ActionResult<List<TrainingDto>>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Ok(await trainingApplicationService.GetAllAsync(cancellationToken));
    }

    /// <summary>
    /// Updates an existing training.
    /// </summary>
    /// <param name="request">The training update request containing updated training details.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 200 OK if the update was successful.
    /// 404 Not Found if the training does not exist.
    /// 400 Bad Request on validation errors.
    /// </returns>
    [Authorize]
    [HttpPut]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(TrainingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult> UpdateTrainingAsync(
        [FromBody] TrainingEditionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await trainingApplicationService.EditAsync(request, cancellationToken);

        return result.Match<ActionResult>(Ok,
            errors =>
            {
                return errors.Any(error => error.ErrorCode == ErrorCode.NotFound)
                    ? NotFound(errors)
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
    public async Task<IActionResult> GetByTrainerIdAsync(Guid trainerId, CancellationToken cancellationToken)
    {
        var result = await trainingApplicationService.GetByTrainerIdAsync(trainerId, cancellationToken);
        return result.Match<IActionResult>(Ok,BadRequest);
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

    [Authorize]
    [HttpDelete("{trainingId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
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
