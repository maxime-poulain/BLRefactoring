using BLRefactoring.DDD.Application.Services.TrainerServices;
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
    [ProducesResponseType(typeof(IErrorCollection), StatusCodes.Status400BadRequest)]
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
    [ProducesResponseType(typeof(IErrorCollection), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(TrainerDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrainerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await _trainerApplicationService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return NotFound(result.Errors);
        }

        return Ok(result.Value);
    }

    [HttpGet("all")]
    [ProducesResponseType(typeof(IErrorCollection), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<TrainerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TrainerDto>>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Ok(await _trainerApplicationService.GetAllAsync(cancellationToken));
    }
}
