using BLRefactoring.Shared.Infrastructure.Http;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Create;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Delete;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.Edit;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetAll;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetById;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetByTopic;
using BLRefactoring.DDDWithCqrs.Application.Features.Trainings.GetByTrainerId;
using BLRefactoring.Shared.Application.Dtos.Training;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.CQS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.DDDWithCqrs.Api.Controller;

public class TrainingController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher)
    : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> CreateTrainingAsync(CreateTrainingCommand command)
    {
        var trainingId = command.TrainingId;
        var result = await commandDispatcher.DispatchAsync(command);

        return result.Match<ActionResult>(
            () => CreatedAtAction("GetTrainingById",
                new { id = command.TrainingId }, trainingId),
            errors => errors.Any(e => e.ErrorCode == ErrorCode.DuplicateTitle)
                ? Conflict(errors)
                : BadRequest(errors));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TrainingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainingDto>> GetTrainingByIdAsync(Guid id)
    {
        var training = await queryDispatcher.DispatchAsync(new GetTrainingByIdQuery(id));

        // Using a monad such Maybe<T,None> could be an alternative
        // to avoid potential null reference exception.
        if (training == null)
        {
            return NotFound();
        }

        // The ETag published here is what the caller must send back as If-Match
        // when they later edit this training.
        this.SetETag(training.RowVersion);
        return Ok(training);
    }

    [HttpGet("all")]
    [ProducesResponseType(typeof(List<TrainingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<TrainingDto>>> GetAllAsync()
    {
        return await queryDispatcher.DispatchAsync(new GetAllTrainingsQuery());
    }

    [Authorize(Policy = "TrainingOwner")]
    [HttpPut("{trainingId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status428PreconditionRequired)]
    public async Task<ActionResult> EditTrainingAsync(
        Guid trainingId,
        [FromBody] EditTrainingCommand command)
    {
        if (!this.TryGetExpectedVersion(out var expectedVersion))
        {
            return this.PreconditionRequired();
        }

        command.TrainingId = trainingId;
        command.ExpectedVersion = expectedVersion;
        var result = await commandDispatcher.DispatchAsync(command);

        return result.Match<ActionResult>(
            () => Ok(),
            errors =>
            {
                if (errors.Any(e => e.ErrorCode == ErrorCode.NotFound))
                {
                    return NotFound();
                }

                if (errors.Any(e => e.ErrorCode == ErrorCode.ConcurrencyConflict))
                {
                    return this.PreconditionFailed(errors);
                }

                return errors.Any(e => e.ErrorCode == ErrorCode.DuplicateTitle)
                    ? Conflict(errors)
                    : BadRequest(errors);
            });
    }

    [HttpGet("by-trainer/{trainerId:guid}")]
    [ProducesResponseType(typeof(List<TrainingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<TrainingDto>>> GetByTrainerIdAsync(Guid trainerId)
    {
        return await queryDispatcher.DispatchAsync(new GetTrainingsByTrainerIdQuery(trainerId));
    }

    [HttpGet("by-topic/{topic}")]
    [ProducesResponseType(typeof(List<TrainingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TrainingDto>>> GetByTopicAsync(string topic)
    {
        return await queryDispatcher.DispatchAsync(new GetTrainingsByTopicQuery(topic));
    }

    [Authorize(Policy = "TrainingOwner")]
    [HttpDelete("{trainingId:guid}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(IEnumerable<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DeleteAsync(Guid trainingId)
    {
        var deletionResult = await commandDispatcher.DispatchAsync(new DeleteTrainingCommand(trainingId));

        return deletionResult.Match<ActionResult>(
            NoContent,
            errors => errors.Any(e => e.ErrorCode == ErrorCode.NotFound) ? NotFound() : BadRequest(errors));
    }
}
