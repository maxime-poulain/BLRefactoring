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
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<ActionResult> CreateTrainingAsync(TrainingCreationRequest request)
    {
        var result = await _trainingApplicationService.CreateAsync(request);

        return result.Match<ActionResult>(
            (trainingDto) => CreatedAtAction("GetTrainingById", new { id = trainingDto.Id }, trainingDto.Id),
            BadRequest);

    }


    [HttpGet("{id}")]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(TrainingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetTrainingByIdAsync(Guid id)
    {
        var result = await _trainingApplicationService.GetByIdAsync(id);

        return result.Match<ActionResult>(trainingDto => Ok(trainingDto),
            errors =>
            {
                return errors.Any(error => error.ErrorCode == ErrorCode.NotFound)
                    ? NotFound(errors)
                    : BadRequest(errors);
            });
    }

    [HttpGet("all")]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<TrainingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TrainingDto>>> GetAllAsync()
    {
        return Ok(await _trainingApplicationService.GetAllAsync());
    }
}
