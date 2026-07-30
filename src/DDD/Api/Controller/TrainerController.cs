using BLRefactoring.DDD.Application.Services.TrainerServices;
using BLRefactoring.Shared.Application.Dtos.Trainer;
using BLRefactoring.Shared.Common.Errors;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.DDD.Api.Controller;

/// <summary>
/// API controller for managing trainer resources.
/// Provides REST endpoints for reading and deleting trainer records.
/// Trainers are only created through the registration flow, which creates
/// the identity user and its trainer atomically.
/// </summary>
/// <param name="trainerApplicationService">Application service for trainer operations.</param>
public class TrainerController(ITrainerApplicationService trainerApplicationService)
    : ApiControllerBase
{
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
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(TrainerDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrainerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await trainerApplicationService.GetByIdAsync(id, cancellationToken);

        return result.Match<ActionResult>(Ok,
            errors =>
                errors.Any(error => error.ErrorCode == ErrorCode.NotFound) ? NotFound() : BadRequest(errors));
    }

    /// <summary>
    /// Retrieves all available trainers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>200 OK with a list of all trainers.</returns>
    [HttpGet("all")]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<TrainerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TrainerDto>>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Ok(await trainerApplicationService.GetAllAsync(cancellationToken));
    }

    /// <summary>
    /// Deletes an existing trainer.
    /// </summary>
    /// <param name="id">The unique identifier of the trainer to delete.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 204 No Content if the deletion was successful.
    /// 404 Not Found if the trainer does not exist.
    /// 400 Bad Request on validation errors.
    /// </returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(List<TrainerDto>), StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await trainerApplicationService.DeleteAsync(id, cancellationToken);

        return result.Match<ActionResult>(NoContent,
            errors => errors.Any(error => error.ErrorCode == ErrorCode.NotFound) ? NotFound() : BadRequest(errors));
    }
}
