using BLRefactoring.DDDWithCqrs.Application.Features.Trainers;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Delete;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetAll;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetById;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.CQS;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.DDDWithCqrs.Api.Controller;

public class TrainerController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher)
    : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<ActionResult> CreateAsync(CreateTrainerCommand request)
    {
        var trainerId = request.TrainerId;
        var result = await commandDispatcher.DispatchAsync(request);

        return result.Match<ActionResult>(
            () => CreatedAtAction("GetById", new { id = trainerId }, trainerId),
            BadRequest);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(TrainerDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrainerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var trainer = await queryDispatcher.DispatchAsync(new GetTrainerByIdQuery(id), cancellationToken);
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
        return Ok(await queryDispatcher.DispatchAsync(new GetAllTrainersQuery(), cancellationToken));
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.DispatchAsync(new DeleteTrainerCommand(id), cancellationToken);

        return result.Match<ActionResult>(
            NoContent,
            errors =>
            {
                if (errors.Any(error => error.ErrorCode == ErrorCode.NotFound))
                {
                    return NotFound();
                }

                return BadRequest(errors);
            }
            );
    }
}
