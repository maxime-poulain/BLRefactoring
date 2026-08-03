using TrainingHub.DDD.Api.Mappings;
using TrainingHub.DDD.Application.Services.TrainerServices;
using TrainingHub.Shared;
using TrainingHub.Shared.Api.Contracts.Mappings;
using TrainingHub.Shared.Api.Contracts.Trainers;
using TrainingHub.Shared.Api.Controllers;
using TrainingHub.Shared.Api.Http;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Common.Errors;
using Microsoft.AspNetCore.Mvc;

namespace TrainingHub.DDD.Api.Controller;

/// <summary>
/// API controller for reading and editing trainer resources.
/// Trainers are only created through the registration flow, which creates the identity user and
/// its trainer atomically, and no endpoint deletes one: removing a trainer is an administrative
/// decision, not something a trainer performs on themselves.
/// </summary>
/// <remarks>
/// The action signatures speak only in API contracts. What the application service accepts and
/// returns is translated on either side, so the published API can change without the service
/// changing, and the reverse.
/// </remarks>
/// <param name="trainerApplicationService">Application service for trainer operations.</param>
/// <param name="currentUserService">Provides the identity of the caller.</param>
public sealed class TrainerController(
    ITrainerApplicationService trainerApplicationService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    /// <summary>
    /// Retrieves the profile of the authenticated trainer.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 200 OK with the profile and the <c>ETag</c> to send back when editing it.
    /// 400 Bad Request on validation errors.
    /// 404 Not Found if the token refers to a trainer that no longer exists.
    /// </returns>
    /// <remarks>
    /// The counterpart of <c>PUT /Trainer/me</c>: since editing requires the version
    /// the caller read, they need a way to read their own profile without knowing
    /// their identifier.
    /// </remarks>
    [HttpGet("me")]
    [ProducesEntityTag]
    [ProducesResponseType(typeof(TrainerResponseHttp), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainerResponseHttp>> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var result = await trainerApplicationService.GetByIdAsync(
            currentUserService.TrainerId, cancellationToken);

        return result.Match<ActionResult>(
            trainer =>
            {
                this.SetETag(trainer.RowVersion);
                return Ok(trainer.ToHttp());
            },
            errors =>
                errors.Any(error => error.ErrorCode == ErrorCodes.NotFound)
                    ? NotFound()
                    : this.Problem(StatusCodes.Status400BadRequest, errors));
    }

    /// <summary>
    /// Replaces the profile of the authenticated trainer.
    /// </summary>
    /// <param name="request">The new state of the profile.</param>
    /// <param name="ifMatch">The version the caller read, as served in the <c>ETag</c>.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 200 OK with the updated trainer.
    /// 400 Bad Request on validation errors.
    /// 404 Not Found if the token refers to a trainer that no longer exists.
    /// </returns>
    /// <remarks>
    /// The trainer being edited is the one carried by the token rather than a route
    /// parameter: a trainer only ever edits their own profile, so there is no
    /// ownership to check and no identifier to tamper with.
    /// Editing the contact email leaves the identity account untouched — it is not
    /// the credential used to sign in.
    /// The request must carry an <c>If-Match</c> holding the <c>ETag</c> returned
    /// when the profile was read, so an edit based on a stale copy is rejected
    /// instead of silently overwriting someone else's changes. It is bound rather than read off
    /// <c>Request.Headers</c>, so that it reaches the OpenAPI document and generated clients can
    /// send it; and nullable, so that its absence is this endpoint's 428 rather than model
    /// validation's 400. See ADR 0010.
    /// </remarks>
    [HttpPut("me")]
    [ProducesEntityTag]
    [ProducesResponseType(typeof(TrainerResponseHttp), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    public async Task<ActionResult<TrainerResponseHttp>> EditCurrentAsync(
        [FromBody] EditTrainerRequestHttp request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        if (!EntityTag.TryParse(ifMatch, out var expectedVersion))
        {
            return this.PreconditionRequired();
        }

        var result = await trainerApplicationService.EditAsync(
            request.ToApplicationRequest(),
            expectedVersion,
            cancellationToken);

        return result.Match<ActionResult>(
            trainer =>
            {
                this.SetETag(trainer.RowVersion);
                return Ok(trainer.ToHttp());
            },
            errors =>
            {
                if (errors.Any(error => error.ErrorCode == ErrorCodes.NotFound))
                {
                    return NotFound();
                }

                return errors.Any(error => error.ErrorCode == ErrorCodes.ConcurrencyConflict)
                    ? this.Problem(StatusCodes.Status412PreconditionFailed, errors)
                    : this.Problem(StatusCodes.Status400BadRequest, errors);
            });
    }

    /// <summary>
    /// Serves a trainer's photo.
    /// </summary>
    /// <param name="id">The trainer whose photo is wanted.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The image, or 404 when the trainer has none.</returns>
    /// <remarks>
    /// By identifier rather than <c>me</c>, unlike the two below. Publishing a portrait is
    /// self-service; looking at one is what a catalogue does, and this is the shape that survives
    /// the day the catalogue is public — <c>[AllowAnonymous]</c> and nothing else.
    /// </remarks>
    [HttpGet("{id:guid}/photo")]
    // byte[], which the document generator renders as `type: string, format: binary` — the schema
    // for "this response is a file". Naming a FileResult here instead described the body as a JSON
    // object with that name, and the generated client dutifully offered to deserialise a portrait.
    [Produces(TrainerPhoto.PngContentType, TrainerPhoto.JpegContentType, TrainerPhoto.WebpContentType)]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetPhotoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var photo = await trainerApplicationService.GetPhotoAsync(id, cancellationToken);

        return photo is null ? NotFound() : this.PhotoFile(photo);
    }

    /// <summary>
    /// Publishes a photo on the calling trainer's profile, replacing any they had.
    /// </summary>
    /// <param name="request">The multipart body carrying the image.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The updated profile, or why the photo was refused.</returns>
    /// <remarks>
    /// One verb for publishing and replacing, because there is no third thing to do to a photo and
    /// no reason a caller should have to know which of the two they are performing. PUT also makes
    /// this idempotent, which matters for a body this size on a connection that may drop: a retry
    /// after a timeout costs an orphaned object, never a wrong answer.
    /// </remarks>
    /// <remarks>
    /// No 413 is declared, and that is a finding rather than an omission. The request size limit
    /// does stop the server reading an arbitrary payload, but a body-read failure inside model
    /// binding never reaches an exception handler: MVC folds it into model state and answers 400
    /// with an unbound file. A handler was written to publish 413 in this API's problem shape, and
    /// the integration suite proved it is never called.
    /// </remarks>
    [HttpPut("me/photo")]
    // Stated rather than inferred. Left to itself the document generator describes a bound model
    // carrying a file as application/x-www-form-urlencoded, and NSwag faithfully generates a client
    // that URL-encodes the bytes — a client that cannot upload a photo, produced from a document
    // that reads fine.
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(TrainerPhotoControllerExtensions.MaxRequestBytes)]
    [ProducesResponseType(typeof(TrainerResponseHttp), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TrainerResponseHttp>> SetPhotoAsync(
        [FromForm] UploadTrainerPhotoRequestHttp request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var content = await request.Photo.ReadAllBytesAsync(cancellationToken);

        var result = await trainerApplicationService.SetPhotoAsync(
            content, request.Photo.ContentType, cancellationToken);

        return result.Match<ActionResult>(
            trainer => Ok(trainer.ToHttp()),
            this.PhotoProblem);
    }

    /// <summary>
    /// Takes the calling trainer's photo down.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Nothing, or why there was nothing to take down.</returns>
    [HttpDelete("me/photo")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> DeletePhotoAsync(CancellationToken cancellationToken = default)
    {
        var result = await trainerApplicationService.RemovePhotoAsync(cancellationToken);

        return result.Match<ActionResult>(
            onSuccess: NoContent,
            onFailure: this.PhotoProblem);
    }
}
