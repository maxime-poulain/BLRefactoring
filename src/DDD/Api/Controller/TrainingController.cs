using BLRefactoring.DDD.Application.Services.TrainingServices;
using BLRefactoring.DDD.Application.Services.TrainingServices.Dtos;
using BLRefactoring.Shared.Common.Errors;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.DDD.Api.Controller;

public class TrainingController : ApiControllerBase
{
    private readonly ITrainingApplicationService _trainingApplicationService;

    public TrainingController(ITrainingApplicationService trainingApplicationService)
    {
        _trainingApplicationService = trainingApplicationService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(IErrorCollection), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<ActionResult> CreateTrainingAsync(TrainingCreationRequest request)
    {
        var training = await _trainingApplicationService.CreateAsync(request);

        if (training.IsFailure)
        {
            return BadRequest(training.Errors);
        }

        return CreatedAtAction("GetTrainingById", new { id = training.Value.Id }, training.Value.Id);
    }


    [HttpGet("{id}")]
    [ProducesResponseType(typeof(IErrorCollection), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(TrainingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrainingDto>> GetTrainingByIdAsync(Guid id)
    {
        var training = await _trainingApplicationService.GetByIdAsync(id);
        if (training.IsFailure)
        {
            return NotFound(training.Errors);
        }
        return Ok(training.Value);
    }

    [HttpGet("all")]
    [ProducesResponseType(typeof(IErrorCollection), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<TrainingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TrainingDto>>> GetAllAsync()
    {
        return Ok(await _trainingApplicationService.GetAllAsync());
    }
}
