using BLRefactoring.DDDWithCqrs.Api.Contracts;
using BLRefactoring.DDDWithCqrs.Api.Mappings;
using BLRefactoring.Shared.Api.Authorization;
using BLRefactoring.Shared.Api.Contracts.Errors;
using BLRefactoring.Shared.Api.Contracts.Mappings;
using BLRefactoring.Shared.Api.Contracts.Trainings;
using BLRefactoring.Shared.Api.Controllers;
using BLRefactoring.Shared.Api.Http;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.CQS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.DDDWithCqrs.Api.Controller;

/// <summary>
/// REST endpoints for trainings, on the CQRS stack.
/// </summary>
/// <remarks>
/// Commands and queries are built by <see cref="HttpToApplicationMappings"/> rather than bound
/// from the request. The edition endpoints in particular used to receive a command straight from
/// the body and then have their route identifier and expected version assigned onto it; the
/// mapping composes the three explicitly instead.
/// </remarks>
public class TrainingController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher)
    : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(IEnumerable<ErrorResponseHttp>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(IEnumerable<ErrorResponseHttp>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> CreateTrainingAsync(
        [FromBody] CreateTrainingRequestHttp request,
        CancellationToken cancellationToken = default)
    {
        var command = request.ToCommand();
        var trainingId = command.TrainingId;

        var result = await commandDispatcher.DispatchAsync(command, cancellationToken);

        return result.Match<ActionResult>(
            () => CreatedAtAction("GetTrainingById", new { id = trainingId }, trainingId),
            errors => errors.Any(e => e.ErrorCode == ErrorCode.DuplicateTitle)
                ? Conflict(errors.ToHttp())
                : BadRequest(errors.ToHttp()));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TrainingResponseHttp), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IEnumerable<ErrorResponseHttp>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainingResponseHttp>> GetTrainingByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var training = await queryDispatcher.DispatchAsync(
            HttpToApplicationMappings.ToGetTrainingByIdQuery(id), cancellationToken);

        // Using a monad such Maybe<T,None> could be an alternative
        // to avoid potential null reference exception.
        if (training == null)
        {
            return NotFound();
        }

        // The ETag published here is what the caller must send back as If-Match
        // when they later edit this training.
        this.SetETag(training.RowVersion);
        return Ok(training.ToHttp());
    }

    /// <summary>
    /// Returns one page of trainings, newest first.
    /// </summary>
    [HttpGet("all")]
    [ProducesResponseType(typeof(PagedResponseHttp<TrainingResponseHttp>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IEnumerable<ErrorResponseHttp>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponseHttp<TrainingResponseHttp>>> GetAllAsync(
        [FromQuery] PaginationRequestHttp pagination,
        CancellationToken cancellationToken = default)
    {
        var page = await queryDispatcher.DispatchAsync(
            pagination.ToGetAllTrainingsQuery(), cancellationToken);

        return Ok(page.ToHttp(trainings => trainings.ToHttp()));
    }

    [Authorize(Policy = TrainingOwnerPolicy.Name)]
    [HttpPut("{trainingId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IEnumerable<ErrorResponseHttp>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(IEnumerable<ErrorResponseHttp>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(IEnumerable<ErrorResponseHttp>), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(IEnumerable<ErrorResponseHttp>), StatusCodes.Status428PreconditionRequired)]
    public async Task<ActionResult> EditTrainingAsync(
        Guid trainingId,
        [FromBody] EditTrainingRequestHttp request,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetExpectedVersion(out var expectedVersion))
        {
            return this.PreconditionRequired();
        }

        var result = await commandDispatcher.DispatchAsync(
            request.ToCommand(trainingId, expectedVersion), cancellationToken);

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
                    return this.PreconditionFailed(errors.ToHttp());
                }

                return errors.Any(e => e.ErrorCode == ErrorCode.DuplicateTitle)
                    ? Conflict(errors.ToHttp())
                    : BadRequest(errors.ToHttp());
            });
    }

    [HttpGet("by-trainer/{trainerId:guid}")]
    [ProducesResponseType(typeof(PagedResponseHttp<TrainingResponseHttp>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IEnumerable<ErrorResponseHttp>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponseHttp<TrainingResponseHttp>>> GetByTrainerIdAsync(
        Guid trainerId,
        [FromQuery] PaginationRequestHttp pagination,
        CancellationToken cancellationToken = default)
    {
        var page = await queryDispatcher.DispatchAsync(
            pagination.ToGetTrainingsByTrainerIdQuery(trainerId), cancellationToken);

        return Ok(page.ToHttp(trainings => trainings.ToHttp()));
    }

    [HttpGet("by-topic/{topic}")]
    [ProducesResponseType(typeof(PagedResponseHttp<TrainingResponseHttp>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponseHttp<TrainingResponseHttp>>> GetByTopicAsync(
        string topic,
        [FromQuery] PaginationRequestHttp pagination,
        CancellationToken cancellationToken = default)
    {
        var page = await queryDispatcher.DispatchAsync(
            pagination.ToGetTrainingsByTopicQuery(topic), cancellationToken);

        return Ok(page.ToHttp(trainings => trainings.ToHttp()));
    }

    [Authorize(Policy = TrainingOwnerPolicy.Name)]
    [HttpDelete("{trainingId:guid}")]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(IEnumerable<ErrorResponseHttp>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DeleteAsync(Guid trainingId, CancellationToken cancellationToken = default)
    {
        var deletionResult = await commandDispatcher.DispatchAsync(
            HttpToApplicationMappings.ToDeleteTrainingCommand(trainingId), cancellationToken);

        return deletionResult.Match<ActionResult>(
            NoContent,
            errors => errors.Any(e => e.ErrorCode == ErrorCode.NotFound) ? NotFound() : BadRequest(errors.ToHttp()));
    }
}
