using TrainingHub.DDD.Api.Mappings;
using TrainingHub.DDD.Application.Services.TrainingServices;
using TrainingHub.Shared.Api.Authorization;
using TrainingHub.Shared.Api.Contracts;
using TrainingHub.Shared.Api.Contracts.Mappings;
using TrainingHub.Shared.Api.Contracts.Pagination;
using TrainingHub.Shared.Api.Contracts.Trainings;
using TrainingHub.Shared.Api.Controllers;
using TrainingHub.Shared.Api.Http;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TrainingHub.DDD.Api.Controller;

/// <summary>
/// API controller for managing training resources.
/// Provides REST endpoints for creating, reading, and updating training records.
/// </summary>
/// <remarks>
/// Requests and responses are the API's own contracts; the application service's requests and
/// read models are reached through <see cref="HttpToApplicationMappings"/> and
/// <see cref="ApplicationToHttpMappings"/>.
/// <para>
/// No <c>[Authorize]</c> here or on the actions: <see cref="ApiControllerBase"/> carries it, which
/// is why it exists. This file used to repeat it on the class and on all but two of its actions,
/// and the repetition is worse than redundant, because it invites the reading that the actions
/// without it are anonymous. They are not. The CQRS twin never repeated it.
/// </para>
/// </remarks>
/// <param name="trainingApplicationService">Application service for training operations.</param>
public sealed class TrainingController(ITrainingApplicationService trainingApplicationService) : ApiControllerBase
{
    /// <summary>
    /// Creates a new training.
    /// </summary>
    /// <param name="request">The training creation request containing training details.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 201 Created with the created training ID on success.
    /// 409 Conflict when a training with the same title already exists for the trainer.
    /// 400 Bad Request with validation errors on failure.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<ActionResult> CreateTrainingAsync(
        [FromBody] CreateTrainingHttpRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await trainingApplicationService.CreateAsync(
            request.ToApplicationRequest(), cancellationToken);

        return result.Match<ActionResult>(
            training => CreatedAtAction("GetTrainingById", new { trainingId = training.Id }, training.Id),
            errors => errors.Any(error => error.ErrorCode == TrainingErrorCodes.DuplicateTitle)
                ? this.Problem(StatusCodes.Status409Conflict, errors)
                : this.Problem(StatusCodes.Status400BadRequest, errors));
    }

    /// <summary>
    /// Retrieves a training by its unique identifier.
    /// </summary>
    /// <param name="trainingId">The training the route names.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 200 OK with the training details if found.
    /// 404 Not Found if the training does not exist.
    /// 400 Bad Request on validation errors.
    /// </returns>
    [HttpGet("{trainingId:guid}")]
    [ProducesEntityTag]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(TrainingHttpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetTrainingByIdAsync(
        [NotEmptyIdentifier] Guid trainingId,
        CancellationToken cancellationToken = default)
    {
        var result = await trainingApplicationService.GetByIdAsync(trainingId, cancellationToken);

        // The ETag published here is what the caller must send back as If-Match
        // when they later edit this training.
        return result.Match<ActionResult>(
            training =>
            {
                this.SetETag(training.RowVersion);
                return Ok(training.ToHttp());
            },
            errors => errors.Any(error => error.ErrorCode == ErrorCodes.NotFound)
                ? NotFound()
                : this.Problem(StatusCodes.Status400BadRequest, errors));
    }

    /// <summary>
    /// Updates an existing training.
    /// </summary>
    /// <param name="trainingId">The training the route names.</param>
    /// <param name="request">The training update request containing updated training details.</param>
    /// <param name="ifMatch">The version the caller read, as served in the <c>ETag</c>.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 200 OK if the update was successful.
    /// 403 Forbidden if the current user is not the training owner.
    /// 404 Not Found if the training does not exist.
    /// 409 Conflict when a training with the same title already exists for the trainer.
    /// 412 Precondition Failed when the training changed since the caller read it.
    /// 428 Precondition Required when the request carries no If-Match header.
    /// 400 Bad Request on validation errors.
    /// </returns>
    /// <remarks>
    /// The request must carry an <c>If-Match</c> holding the <c>ETag</c> returned
    /// when the training was read, so an edit based on a stale copy is rejected
    /// instead of silently overwriting someone else's changes.
    /// <para>
    /// It is bound rather than read off <c>Request.Headers</c>, which is what it used to be. A
    /// header nobody declares does not reach the OpenAPI document, so no generated client can send
    /// one — and the front end's edit page answered 428 every single time. See ADR 0010.
    /// </para>
    /// <para>
    /// <see langword="null"/> is the point of the type: with nullable reference types on, a
    /// non-nullable parameter bound from a header is implicitly required, and a request without one
    /// would be answered 400 by model validation instead of the 428 this endpoint owes it.
    /// </para>
    /// </remarks>
    [Authorize(Policy = TrainingOwnerPolicy.Name)]
    [HttpPut("{trainingId:guid}")]
    [ProducesEntityTag]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(TrainingHttpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
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

        var result = await trainingApplicationService.EditAsync(
            trainingId, request.ToApplicationRequest(), expectedVersion, cancellationToken);

        return result.Match<ActionResult>(
            training =>
            {
                this.SetETag(training.RowVersion);
                return Ok(training.ToHttp());
            },
            errors =>
            {
                if (errors.Any(error => error.ErrorCode == ErrorCodes.NotFound))
                {
                    return NotFound();
                }

                if (errors.Any(error => error.ErrorCode == ErrorCodes.ConcurrencyConflict))
                {
                    return this.Problem(StatusCodes.Status412PreconditionFailed, errors);
                }

                return errors.Any(error => error.ErrorCode == TrainingErrorCodes.DuplicateTitle)
                    ? this.Problem(StatusCodes.Status409Conflict, errors)
                    : this.Problem(StatusCodes.Status400BadRequest, errors);
            });
    }

    /// <summary>
    /// Returns one page of the caller's own trainings, newest first.
    /// </summary>
    /// <param name="pagination">The page asked for; the defaults apply when absent.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>200 OK with one page of the trainings belonging to the authenticated trainer.</returns>
    /// <remarks>
    /// The endpoint a screen listing "my trainings" calls. It takes no identifier — the trainer
    /// comes from the token — so there is no parameter a caller could point at somebody else, and no
    /// filtering left for a client to do after the fact.
    /// <para>
    /// It answers the same page envelope as the CQRS host, from the same contract: this host used to
    /// answer the whole collection as a bare array, and the two documents disagreed on the shape of
    /// one operation — which no generated client can serve. The page is asked of the repository as a
    /// named question; what stays different is how each stack fills it. See ADR 0029.
    /// </para>
    /// <para>
    /// The route says <c>my-trainings</c> rather than <c>me</c>, which it was until the API grew a
    /// second <c>/me</c>: <c>/Trainer/me</c> is a profile and this is a list of trainings, and two
    /// endpoints ending in the same word rewarded a careless reading with the wrong one.
    /// </para>
    /// <para>
    /// The action name matches the CQRS host's, as every action here does: <c>operationId</c> is
    /// <c>Controller_Action</c>, so a name that differed would publish two documents describing one
    /// API under two names. See ADR 0008.
    /// </para>
    /// </remarks>
    [HttpGet("my-trainings")]
    [ProducesResponseType(typeof(PagedHttpResponse<TrainingHttpResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedHttpResponse<TrainingHttpResponse>>> GetMineAsync(
        [FromQuery] PaginationHttpRequest pagination,
        CancellationToken cancellationToken = default)
    {
        var page = await trainingApplicationService.GetMineAsync(
            pagination.ToPageRequest(), cancellationToken);

        return Ok(page.ToHttp(trainings => trainings.ToHttp()));
    }

    /// <summary>
    /// Deletes a training by its unique identifier.
    /// </summary>
    /// <param name="trainingId">The training the route names.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 204 No Content if the deletion was successful.
    /// 404 Not Found if the training does not exist.
    /// 400 Bad Request on validation errors.
    /// </returns>
    [Authorize(Policy = TrainingOwnerPolicy.Name)]
    [HttpDelete("{trainingId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTrainingAsync(
        [NotEmptyIdentifier] Guid trainingId,
        CancellationToken cancellationToken = default)
    {
        var result = await trainingApplicationService.DeleteAsync(trainingId, cancellationToken);

        return result.Match<IActionResult>(
            NoContent,
            errors => errors.Any(error => error.ErrorCode == ErrorCodes.NotFound)
                ? NotFound()
                : this.Problem(StatusCodes.Status400BadRequest, errors));
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
    public async Task<IActionResult> TransferTrainingAsync(
        [NotEmptyIdentifier] Guid trainingId,
        [FromBody] TransferTrainingHttpRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await trainingApplicationService.TransferAsync(
            trainingId, request.RecipientTrainerId, cancellationToken);

        return result.Match<IActionResult>(
            NoContent,
            errors => errors.Any(error => error.ErrorCode == ErrorCodes.NotFound)
                ? NotFound()
                : errors.Any(error => error.ErrorCode == TrainingErrorCodes.DuplicateTitle)
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
    public async Task<IActionResult> PublishTrainingAsync(
        [NotEmptyIdentifier] Guid trainingId,
        CancellationToken cancellationToken = default)
    {
        var result = await trainingApplicationService.PublishAsync(trainingId, cancellationToken);

        return result.Match<IActionResult>(
            NoContent,
            errors => errors.Any(error => error.ErrorCode == ErrorCodes.NotFound)
                ? NotFound()
                : errors.Any(error => error.ErrorCode == TrainingErrorCodes.AlreadyPublished)
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
    public async Task<IActionResult> UnpublishTrainingAsync(
        [NotEmptyIdentifier] Guid trainingId,
        CancellationToken cancellationToken = default)
    {
        var result = await trainingApplicationService.UnpublishAsync(trainingId, cancellationToken);

        return result.Match<IActionResult>(
            NoContent,
            errors => errors.Any(error => error.ErrorCode == ErrorCodes.NotFound)
                ? NotFound()
                : errors.Any(error => error.ErrorCode == TrainingErrorCodes.AlreadyUnpublished)
                    ? this.Problem(StatusCodes.Status409Conflict, errors)
                    : this.Problem(StatusCodes.Status400BadRequest, errors));
    }
}
