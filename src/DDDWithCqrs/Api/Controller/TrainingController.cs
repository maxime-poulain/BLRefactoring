using TrainingHub.DDDWithCqrs.Api.Mappings;
using TrainingHub.Shared.Api.Authorization;
using TrainingHub.Shared.Api.Contracts;
using TrainingHub.Shared.Api.Contracts.Mappings;
using TrainingHub.Shared.Api.Contracts.Pagination;
using TrainingHub.Shared.Api.Contracts.Trainings;
using TrainingHub.Shared.Api.Controllers;
using TrainingHub.Shared.Api.Http;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.CQS;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TrainingHub.DDDWithCqrs.Api.Controller;

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
    /// <summary>
    /// Creates a training owned by the calling trainer.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> CreateTrainingAsync(
        [FromBody] CreateTrainingHttpRequest request,
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

    /// <summary>
    /// Reads one training, publishing its row version as an <c>ETag</c>.
    /// </summary>
    [HttpGet("{trainingId:guid}")]
    [ProducesEntityTag]
    [ProducesResponseType(typeof(TrainingHttpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainingHttpResponse>> GetTrainingByIdAsync(
        [NotEmptyIdentifier] Guid trainingId,
        CancellationToken cancellationToken = default)
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
    /// Returns one page of the caller's own trainings, newest first.
    /// </summary>
    /// <remarks>
    /// The endpoint a screen listing "my trainings" calls. It takes no identifier and passes none:
    /// the query carries paging only, and the handler resolves the trainer from the authenticated
    /// caller. There is nothing here to point at somebody else — not in the route, and not in what
    /// this action dispatches.
    /// <para>
    /// The route says <c>my-trainings</c> rather than <c>me</c>, which it was until the API grew a
    /// second <c>/me</c>: <c>/Trainer/me</c> is a profile and this is a list of trainings, and two
    /// endpoints ending in the same word rewarded a careless reading with the wrong one.
    /// </para>
    /// <para>
    /// <c>[Authorize]</c> is not written here: <see cref="ApiControllerBase"/> carries it for every
    /// action of every controller, which is why an unauthenticated call to this one is a 401 before
    /// the action is reached. Repeating it would suggest the others are open.
    /// </para>
    /// </remarks>
    [HttpGet("my-trainings")]
    [ProducesResponseType(typeof(PagedHttpResponse<TrainingHttpResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedHttpResponse<TrainingHttpResponse>>> GetMineAsync(
        [FromQuery] PaginationHttpRequest pagination,
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
    [ProducesResponseType(typeof(TrainingHttpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    public async Task<ActionResult> UpdateTrainingAsync(
        [NotEmptyIdentifier] Guid trainingId,
        [FromBody] EditTrainingHttpRequest request,
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

    /// <summary>
    /// Deletes a training the caller owns.
    /// </summary>
    [Authorize(Policy = TrainingOwnerPolicy.Name)]
    [HttpDelete("{trainingId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DeleteTrainingAsync(
        [NotEmptyIdentifier] Guid trainingId,
        CancellationToken cancellationToken = default)
    {
        var deletionResult = await commandDispatcher.DispatchAsync(
            HttpToApplicationMappings.ToDeleteTrainingCommand(trainingId), cancellationToken);

        return deletionResult.Match<ActionResult>(
            NoContent,
            errors => errors.Any(e => e.ErrorCode == ErrorCodes.NotFound) ? NotFound() : this.Problem(StatusCodes.Status400BadRequest, errors));
    }

    /// <summary>
    /// Hands a training over to another trainer (ADR 0036).
    /// </summary>
    /// <remarks>
    /// No If-Match, mirroring delete rather than edit: a transfer is an action on the resource,
    /// not an edit of its content, and the contention that matters — the recipient's capacity and
    /// titles — is checked by the domain service at the moment of the decision.
    /// </remarks>
    /// <param name="trainingId">The training the route names.</param>
    /// <param name="request">The transfer request naming the recipient.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 204 No Content if the transfer succeeded — nothing is created, and the giver can no longer read the training.
    /// 400 Bad Request when the recipient is the current owner, unknown, or at capacity.
    /// 404 Not Found if the training does not exist.
    /// 409 Conflict when the recipient already has a training under the same title.
    /// </returns>
    [Authorize(Policy = TrainingOwnerPolicy.Name)]
    [HttpPost("{trainingId:guid}/transfer")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> TransferTrainingAsync(
        [NotEmptyIdentifier] Guid trainingId,
        [FromBody] TransferTrainingHttpRequest request,
        CancellationToken cancellationToken = default)
    {
        var transferResult = await commandDispatcher.DispatchAsync(
            request.ToCommand(trainingId), cancellationToken);

        return transferResult.Match<ActionResult>(
            NoContent,
            errors => errors.Any(e => e.ErrorCode == ErrorCodes.NotFound)
                ? NotFound()
                : errors.Any(e => e.ErrorCode == TrainingErrorCodes.DuplicateTitle)
                    ? this.Problem(StatusCodes.Status409Conflict, errors)
                    : this.Problem(StatusCodes.Status400BadRequest, errors));
    }

    /// <summary>
    /// Offers a withdrawn training to the public again (ADR 0050).
    /// </summary>
    /// <remarks>
    /// No If-Match, mirroring delete and transfer rather than edit: this is an action on the
    /// resource, not an edit of its content. The race it could lose is the only one worth guarding,
    /// and the aggregate already refuses it by name — publishing a training that is published
    /// answers 409 rather than quietly succeeding, so a caller that lost the race is told so.
    /// </remarks>
    /// <param name="trainingId">The training the route names.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 204 No Content when the training is now on offer.
    /// 400 Bad Request when the owner is suspended or their catalogue is full.
    /// 404 Not Found if the training does not exist.
    /// 409 Conflict when the training was already published.
    /// </returns>
    [Authorize(Policy = TrainingOwnerPolicy.Name)]
    [HttpPost("{trainingId:guid}/publish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> PublishTrainingAsync(
        [NotEmptyIdentifier] Guid trainingId,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.DispatchAsync(
            HttpToApplicationMappings.ToPublishTrainingCommand(trainingId), cancellationToken);

        return result.Match<ActionResult>(
            NoContent,
            errors => errors.Any(e => e.ErrorCode == ErrorCodes.NotFound)
                ? NotFound()
                : errors.Any(e => e.ErrorCode == TrainingErrorCodes.AlreadyPublished)
                    ? this.Problem(StatusCodes.Status409Conflict, errors)
                    : this.Problem(StatusCodes.Status400BadRequest, errors));
    }

    /// <summary>
    /// Withdraws a training from public view, keeping it in its owner's listing (ADR 0050).
    /// </summary>
    /// <remarks>
    /// The everyday act that took the place delete used to hold in the interface. Deleting survives
    /// on this API and answers what withdrawing cannot: the training created by mistake, and a
    /// trainer's right to have their data removed.
    /// </remarks>
    /// <param name="trainingId">The training the route names.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 204 No Content when the training has left public view.
    /// 400 Bad Request on validation errors.
    /// 404 Not Found if the training does not exist.
    /// 409 Conflict when the training was already withdrawn.
    /// </returns>
    [Authorize(Policy = TrainingOwnerPolicy.Name)]
    [HttpPost("{trainingId:guid}/unpublish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> UnpublishTrainingAsync(
        [NotEmptyIdentifier] Guid trainingId,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.DispatchAsync(
            HttpToApplicationMappings.ToUnpublishTrainingCommand(trainingId), cancellationToken);

        return result.Match<ActionResult>(
            NoContent,
            errors => errors.Any(e => e.ErrorCode == ErrorCodes.NotFound)
                ? NotFound()
                : errors.Any(e => e.ErrorCode == TrainingErrorCodes.AlreadyUnpublished)
                    ? this.Problem(StatusCodes.Status409Conflict, errors)
                    : this.Problem(StatusCodes.Status400BadRequest, errors));
    }
}
