using BLRefactoring.DDD.Application.Services.TrainerServices;
using BLRefactoring.DDD.Application.Services.TrainerServices.Dto;
using BLRefactoring.Shared.Common.Errors;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.DDD.Api.Controller;

public class TrainerController(ITrainerApplicationService trainerApplicationService)
    : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(TrainerDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TrainerDto>> CreateAsync(TrainerCreationRequest request)
    {
        var result = await trainerApplicationService.CreateAsync(request);

        return result.Match<ActionResult>(
            trainerDto => CreatedAtAction("GetById", new { id = trainerDto.Id }, trainerDto),
            BadRequest);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(TrainerDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrainerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await trainerApplicationService.GetByIdAsync(id, cancellationToken);

        return result.Match<ActionResult>(Ok,
            errors => errors.Any(error => error.ErrorCode == ErrorCode.NotFound) ? NotFound() : BadRequest(errors));
    }

    [HttpGet("all")]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<TrainerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TrainerDto>>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Ok(await trainerApplicationService.GetAllAsync(cancellationToken));
    }

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
