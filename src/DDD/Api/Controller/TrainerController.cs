using BLRefactoring.DDD.Application.Services.TrainerServices;
using BLRefactoring.DDD.Application.Services.TrainerServices.Dto;
using BLRefactoring.Shared.Common.Errors;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.DDD.Api.Controller;

public class TrainerController : ApiControllerBase
{
    private readonly ITrainerApplicationService _trainerApplicationService;

    public TrainerController(ITrainerApplicationService trainerApplicationService)
    {
        _trainerApplicationService = trainerApplicationService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(TrainerDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TrainerDto>> CreateAsync(TrainerCreationRequest request)
    {
        var result = await _trainerApplicationService.CreateAsync(request);

        if (result.IsFailure)
        {
            return BadRequest(result.Errors);
        }

        return CreatedAtAction("GetById", new { id = result.Value.Id }, result.Value);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(TrainerDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrainerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await _trainerApplicationService.GetByIdAsync(id, cancellationToken);

        return result.IsFailure switch
        {
            true when result.Errors.Any(error => error.ErrorCode == ErrorCode.NotFound) => NotFound(),
            true => BadRequest(result.Errors),
            _ => Ok(result.Value)
        };
    }

    [HttpGet("all")]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<TrainerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TrainerDto>>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Ok(await _trainerApplicationService.GetAllAsync(cancellationToken));
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(List<TrainerDto>), StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await _trainerApplicationService.DeleteAsync(id, cancellationToken);

        return result.IsFailure switch
        {
            true when result.Errors.Any(error => error.ErrorCode == ErrorCode.NotFound) =>
                NotFound(),
            true => BadRequest(result.Errors),
            _ => NoContent()
        };
    }

}
