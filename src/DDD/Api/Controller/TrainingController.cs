using BLRefactoring.DDD.Api.Mappings;
using BLRefactoring.DDD.Application.Services.TrainingServices;
using BLRefactoring.Shared.Api.Authorization;
using BLRefactoring.Shared.Api.Contracts.Mappings;
using BLRefactoring.Shared.Api.Contracts.Trainings;
using BLRefactoring.Shared.Api.Controllers;
using BLRefactoring.Shared.Api.Http;
using BLRefactoring.Shared.Common.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BLRefactoring.DDD.Api.Controller;

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
/// is why it exists. This file used to repeat it six times — once on the class and once on five of
/// the seven actions — and the repetition is worse than redundant, because it invites the reading
/// that the two actions without it are anonymous. They are not. The CQRS twin never repeated it.
/// </para>
/// </remarks>
/// <param name="trainingApplicationService">Application service for training operations.</param>
public class TrainingController(ITrainingApplicationService trainingApplicationService) : ApiControllerBase
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
        [FromBody] CreateTrainingRequestHttp request,
        CancellationToken cancellationToken = default)
    {
        var result = await trainingApplicationService.CreateAsync(
            request.ToApplicationRequest(), cancellationToken);

        return result.Match<ActionResult>(
            training => CreatedAtAction("GetTrainingById", new { id = training.Id }, training.Id),
            errors => errors.Any(error => error.ErrorCode == ErrorCode.DuplicateTitle)
                ? this.Problem(StatusCodes.Status409Conflict, errors)
                : this.Problem(StatusCodes.Status400BadRequest, errors));
    }

    /// <summary>
    /// Retrieves a training by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the training.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 200 OK with the training details if found.
    /// 404 Not Found if the training does not exist.
    /// 400 Bad Request on validation errors.
    /// </returns>
    [HttpGet("{id}")]
    [ProducesEntityTag]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(TrainingResponseHttp), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetTrainingByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await trainingApplicationService.GetByIdAsync(id, cancellationToken);

        // The ETag published here is what the caller must send back as If-Match
        // when they later edit this training.
        return result.Match<ActionResult>(
            training =>
            {
                this.SetETag(training.RowVersion);
                return Ok(training.ToHttp());
            },
            errors => errors.Any(error => error.ErrorCode == ErrorCode.NotFound)
                ? NotFound()
                : this.Problem(StatusCodes.Status400BadRequest, errors));
    }

    /// <summary>
    /// Retrieves all available trainings.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>200 OK with a list of all trainings.</returns>
    /// <remarks>
    /// The only answer this action has. It takes nothing to bind and reaches nothing that fails,
    /// so the 400 it used to advertise described a response no caller could ever receive.
    /// </remarks>
    [HttpGet("all")]
    [ProducesResponseType(typeof(List<TrainingResponseHttp>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TrainingResponseHttp>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var trainings = await trainingApplicationService.GetAllAsync(cancellationToken);
        return Ok(trainings.ToHttp());
    }

    /// <summary>
    /// Updates an existing training.
    /// </summary>
    /// <param name="trainingId">The unique identifier of the training to update.</param>
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
    [ProducesResponseType(typeof(TrainingResponseHttp), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
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
                if (errors.Any(error => error.ErrorCode == ErrorCode.NotFound))
                {
                    return NotFound();
                }

                if (errors.Any(error => error.ErrorCode == ErrorCode.ConcurrencyConflict))
                {
                    return this.Problem(StatusCodes.Status412PreconditionFailed, errors);
                }

                return errors.Any(error => error.ErrorCode == ErrorCode.DuplicateTitle)
                    ? this.Problem(StatusCodes.Status409Conflict, errors)
                    : this.Problem(StatusCodes.Status400BadRequest, errors);
            });
    }

    /// <summary>
    /// Retrieves all trainings associated with a specific trainer.
    /// </summary>
    /// <param name="trainerId">The unique identifier of the trainer whose trainings are to be retrieved.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 200 OK with a list of trainings associated with the trainer.
    /// 400 Bad Request when the route identifier is not a well-formed <c>Guid</c>.
    /// </returns>
    [HttpGet("by-trainer/{trainerId:guid}")]
    [ProducesResponseType(typeof(List<TrainingResponseHttp>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<TrainingResponseHttp>>> GetByTrainerIdAsync(
        Guid trainerId,
        CancellationToken cancellationToken = default)
    {
        var trainings = await trainingApplicationService.GetByTrainerIdAsync(trainerId, cancellationToken);

        return Ok(trainings.ToHttp());
    }

    /// <summary>
    /// Retrieves all trainings that match the specified topic.
    /// </summary>
    /// <param name="topic">The topic name to filter trainings by.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>200 OK with a list of trainings matching the topic.</returns>
    [HttpGet("by-topic/{topic}")]
    [ProducesResponseType(typeof(List<TrainingResponseHttp>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByTopicAsync(string topic, CancellationToken cancellationToken = default)
    {
        var trainings = await trainingApplicationService.GetByTopicAsync(topic, cancellationToken);
        return Ok(trainings.ToHttp());
    }

    /// <summary>
    /// Deletes a training by its unique identifier.
    /// </summary>
    /// <param name="trainingId">The unique identifier of the training to delete.</param>
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
    public async Task<IActionResult> DeleteTrainingAsync(Guid trainingId, CancellationToken cancellationToken = default)
    {
        var result = await trainingApplicationService.DeleteAsync(trainingId, cancellationToken);

        return result.Match<IActionResult>(
            NoContent,
            errors => errors.Any(error => error.ErrorCode == ErrorCode.NotFound)
                ? NotFound()
                : this.Problem(StatusCodes.Status400BadRequest, errors));
    }
}
