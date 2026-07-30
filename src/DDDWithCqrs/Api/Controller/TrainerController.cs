using BLRefactoring.DDDWithCqrs.Application.Features.Trainers;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Delete;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetAll;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainers.GetById;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.CQS;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.DDDWithCqrs.Api.Controller;

/// <summary>
/// Trainers are only created through the registration flow, which creates
/// the identity user and its trainer atomically.
/// </summary>
public class TrainerController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher)
    : ApiControllerBase
{
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

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteAsync([FromRoute] Guid id, CancellationToken cancellationToken)
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
