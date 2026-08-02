using BLRefactoring.DDDWithCqrs.Api.Contracts;
using BLRefactoring.DDDWithCqrs.Api.Mappings;
using BLRefactoring.Shared.Api.Authorization;
using BLRefactoring.Shared.Api.Contracts.Mappings;
using BLRefactoring.Shared.Api.Contracts.Trainings;
using BLRefactoring.Shared.Api.Controllers;
using BLRefactoring.Shared.Api.Http;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.CQS;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
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
/// <para>
/// The action names match the layered host's, method for method, and that is a contract rather
/// than a style choice: <c>operationId</c> is <c>Controller_Action</c>, so this host published
/// <c>Training_EditTraining</c> and <c>Training_Delete</c> where the other published
/// <c>Training_UpdateTraining</c> and <c>Training_DeleteTraining</c>. Two documents describing the
/// same API disagreed on the name of two of its operations, which falsifies ADR 0006's headline
/// consequence — that one generated client fits both hosts. See ADR 0008 on why renaming a method
/// here is a published change rather than an internal one.
/// </para>
/// </remarks>
public sealed class TrainingController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher)
    : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> CreateTrainingAsync(
        [FromBody] CreateTrainingRequestHttp request,
        CancellationToken cancellationToken = default)
    {
        var command = request.ToCommand();
        var trainingId = command.TrainingId;

        var result = await commandDispatcher.DispatchAsync(command, cancellationToken);

        return result.Match<ActionResult>(
            () => CreatedAtAction("GetTrainingById", new { trainingId }, trainingId),
            errors => errors.Any(e => e.ErrorCode == TrainingErrorCodes.DuplicateTitle)
                ? this.Problem(StatusCodes.Status409Conflict, errors)
                : this.Problem(StatusCodes.Status400BadRequest, errors));
    }

    [HttpGet("{trainingId:guid}")]
    [ProducesEntityTag]
    [ProducesResponseType(typeof(TrainingResponseHttp), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainingResponseHttp>> GetTrainingByIdAsync(Guid trainingId, CancellationToken cancellationToken = default)
    {
        var training = await queryDispatcher.DispatchAsync(
            HttpToApplicationMappings.ToGetTrainingByIdQuery(trainingId), cancellationToken);

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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponseHttp<TrainingResponseHttp>>> GetAllAsync(
        [FromQuery] PaginationRequestHttp pagination,
        CancellationToken cancellationToken = default)
    {
        var page = await queryDispatcher.DispatchAsync(
            pagination.ToGetAllTrainingsQuery(), cancellationToken);

        return Ok(page.ToHttp(trainings => trainings.ToHttp()));
    }

    /// <summary>
    /// Returns one page of the caller's own trainings, newest first.
    /// </summary>
    /// <remarks>
    /// The counterpart of <c>GET /Trainer/me</c>, and the endpoint a screen listing "my trainings"
    /// is meant to call. It takes no identifier and passes none: the query carries paging only, and
    /// the handler resolves the trainer from the authenticated caller. Unlike
    /// <c>by-trainer/{trainerId}</c> there is nothing here to point at somebody else — not in the
    /// route, and not in what this action dispatches.
    /// <para>
    /// <c>[Authorize]</c> is not written here: <see cref="ApiControllerBase"/> carries it for every
    /// action of every controller, which is why an unauthenticated call to this one is a 401 before
    /// the action is reached. Repeating it would suggest the others are open.
    /// </para>
    /// </remarks>
    [HttpGet("me")]
    [ProducesResponseType(typeof(PagedResponseHttp<TrainingResponseHttp>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponseHttp<TrainingResponseHttp>>> GetMineAsync(
        [FromQuery] PaginationRequestHttp pagination,
        CancellationToken cancellationToken = default)
    {
        var page = await queryDispatcher.DispatchAsync(
            pagination.ToGetMyTrainingsQuery(), cancellationToken);

        return Ok(page.ToHttp(trainings => trainings.ToHttp()));
    }

    /// <remarks>
    /// <c>If-Match</c> is bound rather than read off <c>Request.Headers</c>, so that it reaches the
    /// OpenAPI document and generated clients can send it; and nullable, so that its absence is
    /// this endpoint's 428 rather than model validation's 400. See ADR 0010.
    /// <para>
    /// The command reports success and nothing more, so the updated representation is read back
    /// through the query side — the same shape <c>TrainerController.EditCurrentAsync</c> uses on
    /// this host. It used to answer a bare <c>200</c> with no body and no <c>ETag</c>, which left a
    /// caller holding a version that had just been superseded: editing twice in a row meant a
    /// guaranteed 412 and a mandatory extra GET. That was the odd one out among the four editing
    /// endpoints, not a property of CQRS.
    /// </para>
    /// </remarks>
    [Authorize(Policy = TrainingOwnerPolicy.Name)]
    [HttpPut("{trainingId:guid}")]
    [ProducesEntityTag]
    [ProducesResponseType(typeof(TrainingResponseHttp), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    public async Task<ActionResult> UpdateTrainingAsync(
        Guid trainingId,
        [FromBody] EditTrainingRequestHttp request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        if (!EntityTag.TryParse(ifMatch, out var expectedVersion))
        {
            return this.PreconditionRequired();
        }

        var result = await commandDispatcher.DispatchAsync(
            request.ToCommand(trainingId, expectedVersion), cancellationToken);

        return await result.MatchAsync<ActionResult>(
            onSuccess: async () =>
            {
                var training = await queryDispatcher.DispatchAsync(
                    HttpToApplicationMappings.ToGetTrainingByIdQuery(trainingId), cancellationToken);

                if (training is null)
                {
                    return NotFound();
                }

                // The ETag published here is what the caller must send back to edit again.
                // Without it every second edit was a 412 the caller could do nothing about but
                // re-read.
                this.SetETag(training.RowVersion);
                return Ok(training.ToHttp());
            },
            onFailure: errors => ValueTask.FromResult<ActionResult>(
                errors.Any(e => e.ErrorCode == ErrorCodes.NotFound) ? NotFound()
                : errors.Any(e => e.ErrorCode == ErrorCodes.ConcurrencyConflict) ? this.Problem(StatusCodes.Status412PreconditionFailed, errors)
                : errors.Any(e => e.ErrorCode == TrainingErrorCodes.DuplicateTitle) ? this.Problem(StatusCodes.Status409Conflict, errors)
                : this.Problem(StatusCodes.Status400BadRequest, errors)));
    }

    [HttpGet("by-trainer/{trainerId:guid}")]
    [ProducesResponseType(typeof(PagedResponseHttp<TrainingResponseHttp>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DeleteTrainingAsync(Guid trainingId, CancellationToken cancellationToken = default)
    {
        var deletionResult = await commandDispatcher.DispatchAsync(
            HttpToApplicationMappings.ToDeleteTrainingCommand(trainingId), cancellationToken);

        return deletionResult.Match<ActionResult>(
            NoContent,
            errors => errors.Any(e => e.ErrorCode == ErrorCodes.NotFound) ? NotFound() : this.Problem(StatusCodes.Status400BadRequest, errors));
    }
}
