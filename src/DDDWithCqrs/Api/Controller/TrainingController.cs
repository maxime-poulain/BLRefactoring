using BLRefactoring.DDDWithCqrs.Application.Features.Trainings;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Create;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Delete;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetAll;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetById;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.CQS;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.DDDWithCqrs.Api.Controller;

public class TrainingController : ApiControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IQueryDispatcher _queryDispatcher;

    public TrainingController(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
    {
        _commandDispatcher = commandDispatcher;
        _queryDispatcher = queryDispatcher;
    }

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CreateTrainingAsync(CreateTrainingCommand command)
    {
        var trainingId = command.TrainingId;
        var result = await _commandDispatcher.DispatchAsync(command);

        return result.Match<ActionResult>(
            () => CreatedAtAction("GetTrainingById",
                new { id = command.TrainingId }, trainingId), BadRequest);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TrainingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainingDto>> GetTrainingByIdAsync(Guid id)
    {
        var training = await _queryDispatcher.DispatchAsync(new GetTrainingByIdQuery(id));

        // Using a monad such Maybe<T,None> could be an alternative
        // to avoid potential null reference exception.
        if (training == null)
        {
            return NotFound();
        }
        return Ok(training);
    }

    [HttpGet("all")]
    [ProducesResponseType(typeof(List<TrainingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<TrainingDto>>> GetAllAsync()
    {
        return await _queryDispatcher.DispatchAsync(new GetAllTrainingsQuery());
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteAsync(Guid id)
    {
        var deletionResult = await _commandDispatcher.DispatchAsync(new DeleteTrainingCommand(id));

        return deletionResult.Match<ActionResult>(
            NoContent,
            errors => errors.Any(e => e.ErrorCode == ErrorCode.NotFound) ? NotFound() : BadRequest(errors));
    }
}
