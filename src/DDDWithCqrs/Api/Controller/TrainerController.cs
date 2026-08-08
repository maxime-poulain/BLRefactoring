using TrainingHub.DDDWithCqrs.Api.Mappings;
using TrainingHub.DDDWithCqrs.Application.Features.Trainers.GetPhoto;
using TrainingHub.DDDWithCqrs.Application.Features.Trainers.RemovePhoto;
using TrainingHub.DDDWithCqrs.Application.Features.Trainers.SetPhoto;
using TrainingHub.Shared;
using TrainingHub.Shared.Api.Authorization;
using TrainingHub.Shared.Api.Contracts;
using TrainingHub.Shared.Api.Contracts.Mappings;
using TrainingHub.Shared.Api.Contracts.Trainers;
using TrainingHub.Shared.Api.Controllers;
using TrainingHub.Shared.Api.Http;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.CQS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TrainingHub.DDDWithCqrs.Api.Controller;

/// <summary>
/// Trainers are only created through the registration flow, which creates
/// the identity user and its trainer atomically.
/// </summary>
/// <remarks>
/// No command or query appears in this file. They are built by
/// <see cref="HttpToApplicationMappings"/> from the API's own contracts, which is what lets the
/// published API and the CQRS messages change independently — and what removed the
/// <c>[JsonIgnore]</c> attributes the commands used to need to be bound from a request body.
/// </remarks>
public sealed class TrainerController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    /// <summary>
    /// Retrieves the profile of the authenticated trainer.
    /// </summary>
    /// <remarks>
    /// The counterpart of <c>PUT /Trainer/me</c>: since editing requires the version
    /// the caller read, they need a way to read their own profile without knowing
    /// their identifier.
    /// </remarks>
    [HttpGet("me")]
    [ProducesEntityTag]
    [ProducesResponseType(typeof(TrainerHttpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainerHttpResponse>> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var trainer = await queryDispatcher.DispatchAsync(
            HttpToApplicationMappings.ToGetTrainerByIdQuery(currentUserService.TrainerId), cancellationToken);

        if (trainer is null)
        {
            return NotFound();
        }

        this.SetETag(trainer.RowVersion);
        return Ok(trainer.ToHttp());
    }

    /// <summary>
    /// Replaces the profile of the authenticated trainer.
    /// </summary>
    /// <remarks>
    /// The trainer being edited is the one carried by the token rather than a route
    /// parameter: a trainer only ever edits their own profile, so there is no
    /// ownership to check and no identifier to tamper with.
    /// Editing the contact email leaves the identity account untouched — it is not
    /// the credential used to sign in.
    /// The command answers whether the write succeeded; the updated representation
    /// is then read back through the query side rather than returned by the command.
    /// <para>
    /// <c>If-Match</c> is bound rather than read off <c>Request.Headers</c>, so that it reaches the
    /// OpenAPI document and generated clients can send it; and nullable, so that its absence is
    /// this endpoint's 428 rather than model validation's 400. See ADR 0010.
    /// </para>
    /// </remarks>
    [Authorize(Policy = ActiveTrainerPolicy.Name)]
    [HttpPut("me")]
    [ProducesEntityTag]
    [ProducesResponseType(typeof(TrainerHttpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    public async Task<ActionResult<TrainerHttpResponse>> EditCurrentAsync(
        [FromBody] EditTrainerHttpRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        if (!EntityTag.TryParse(ifMatch, out var expectedVersion))
        {
            return this.PreconditionRequired();
        }

        var result = await commandDispatcher.DispatchAsync(
            request.ToCommand(expectedVersion), cancellationToken);

        // Still needed for the read-back below, and only for it: GetTrainerByIdQuery is
        // addressed by identifier, so it cannot drop its parameter the way the command did.
        var trainerId = currentUserService.TrainerId;

        return await result.MatchAsync<ActionResult>(
            onSuccess: async () =>
            {
                var trainer = await queryDispatcher.DispatchAsync(
                    HttpToApplicationMappings.ToGetTrainerByIdQuery(trainerId), cancellationToken);

                if (trainer is null)
                {
                    return NotFound();
                }

                this.SetETag(trainer.RowVersion);
                return Ok(trainer.ToHttp());
            },
            onFailure: errors => ValueTask.FromResult<ActionResult>(
                errors.Any(error => error.ErrorCode == ErrorCodes.NotFound) ? NotFound()
                : errors.Any(error => error.ErrorCode == ErrorCodes.ConcurrencyConflict) ? this.Problem(StatusCodes.Status412PreconditionFailed, errors)
                : this.Problem(StatusCodes.Status400BadRequest, errors)));
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
    public async Task<ActionResult> GetPhotoAsync(
        [NotEmptyIdentifier] Guid id,
        CancellationToken cancellationToken = default)
    {
        var photo = await queryDispatcher.DispatchAsync(
            new GetTrainerPhotoQuery(id), cancellationToken);

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
    [Authorize(Policy = ActiveTrainerPolicy.Name)]
    [HttpPut("me/photo")]
    // Stated rather than inferred. Left to itself the document generator describes a bound model
    // carrying a file as application/x-www-form-urlencoded, and NSwag faithfully generates a client
    // that URL-encodes the bytes — a client that cannot upload a photo, produced from a document
    // that reads fine.
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(TrainerPhotoControllerExtensions.MaxRequestBytes)]
    [ProducesResponseType(typeof(TrainerHttpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TrainerHttpResponse>> SetPhotoAsync(
        [FromForm] UploadTrainerPhotoHttpRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var content = await request.Photo.ReadAllBytesAsync(cancellationToken);

        var result = await commandDispatcher.DispatchAsync(
            new SetTrainerPhotoCommand { Content = content, ContentType = request.Photo.ContentType },
            cancellationToken);

        var trainerId = currentUserService.TrainerId;

        return await result.MatchAsync<ActionResult>(
            onSuccess: async () =>
            {
                var trainer = await queryDispatcher.DispatchAsync(
                    HttpToApplicationMappings.ToGetTrainerByIdQuery(trainerId), cancellationToken);

                return trainer is null ? NotFound() : Ok(trainer.ToHttp());
            },
            onFailure: errors => ValueTask.FromResult(this.PhotoProblem(errors)));
    }

    /// <summary>
    /// Takes the calling trainer's photo down.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Nothing, or why there was nothing to take down.</returns>
    [Authorize(Policy = ActiveTrainerPolicy.Name)]
    [HttpDelete("me/photo")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> DeletePhotoAsync(CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.DispatchAsync(
            new RemoveTrainerPhotoCommand(), cancellationToken);

        return result.Match<ActionResult>(
            onSuccess: NoContent,
            onFailure: this.PhotoProblem);
    }
}
