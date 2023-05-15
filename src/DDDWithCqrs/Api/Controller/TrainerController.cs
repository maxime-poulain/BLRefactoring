using BLRefactoring.DDDWithCqrs.Application.Features.Trainers;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Delete;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetAll;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetById;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.CQS;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.DDDWithCqrs.Api.Controller;

public class TrainerController : ApiControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IQueryDispatcher _queryDispatcher;

    public TrainerController(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
    {
        _commandDispatcher = commandDispatcher;
        _queryDispatcher = queryDispatcher;
    }

    [HttpPost]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<ActionResult<TrainerDto>> CreateAsync(CreateTrainerCommand request)
    {
        var trainerId = request.TrainerId;
        var result = await _commandDispatcher.DispatchAsync(request);

        if (result.IsFailure)
        {
            return BadRequest(result.Errors);
        }

        return CreatedAtAction("GetById", new { id = trainerId }, trainerId);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(TrainerDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrainerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var trainer = await _queryDispatcher.DispatchAsync(new GetTrainerByIdQuery(id), cancellationToken);
        if (trainer is null)
        {
            return NotFound();
        }

        return Ok(trainer);
    }

    [HttpGet("all")]
    [ProducesResponseType(typeof(List<TrainerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<TrainerDto>>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Ok(await _queryDispatcher.DispatchAsync(new GetAllTrainersQuery(), cancellationToken));
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await _commandDispatcher.DispatchAsync(new DeleteTrainerCommand(id), cancellationToken);

        switch (result.IsFailure)
        {
            case true when result.Errors.Any(error => error.ErrorCode == ErrorCode.NotFound):
                return NotFound();
            case true:
                return BadRequest(result.Errors);
            default:
                return NoContent();
        }
    }
}
