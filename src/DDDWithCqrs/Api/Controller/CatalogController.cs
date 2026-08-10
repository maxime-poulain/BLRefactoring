using TrainingHub.Shared.Api.Contracts;
using TrainingHub.Shared.Api.Contracts.Catalog;
using TrainingHub.Shared.Api.Contracts.Mappings;
using TrainingHub.Shared.Api.Contracts.Pagination;
using TrainingHub.Shared.Api.Controllers;
using TrainingHub.Shared.Api.Http;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.DDDWithCqrs.Api.Mappings;
using TrainingHub.Shared.CQS;
using Microsoft.AspNetCore.Mvc;

namespace TrainingHub.DDDWithCqrs.Api.Controller;

/// <summary>
/// The public catalog, on the CQRS stack.
/// </summary>
/// <remarks>
/// The only controller in this API that anybody may call. What decides what it may answer is the
/// search index rather than the trainings table, which is what makes an anonymous read defensible
/// at all: the index holds what a visitor may be shown and nothing else (ADR 0059).
/// <para>
/// Three kinds of read, and they read different places for the same reason. The search and the
/// facets answer from the index alone, because titles and topics are all the index holds. The two
/// details — a training's page, and its author's (ADR 0070) — take their <em>visibility</em> from
/// the index and their <em>content</em> from the write model, because a description copied into an
/// index is a description that goes stale (ADR 0062). The portraits are the details' shape applied
/// to bytes, and add one condition of their own: what was never stripped is never published
/// (ADR 0063).
/// </para>
/// </remarks>
public sealed class CatalogController(IQueryDispatcher queryDispatcher) : CatalogControllerBase
{
    /// <summary>
    /// Searches the offered catalog by title, or lists it when no term is given.
    /// </summary>
    /// <remarks>
    /// A training answers when its title matches every word of the term, each word by prefix. The
    /// asymmetry with <c>GET /Administration/trainings</c> is deliberate and recorded: that listing
    /// has no term because a moderator needs the states this index refuses to hold (ADR 0055,
    /// ADR 0059).
    /// </remarks>
    /// <param name="search">The term, from the query string.</param>
    /// <param name="pagination">The page asked for.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 200 OK with one page of trainings on offer.
    /// 400 Bad Request when the term is too long, the topic or the sort is unknown, or the page is
    /// out of range.
    /// </returns>
    [HttpGet("trainings")]
    [ProducesResponseType(typeof(PagedHttpResponse<CatalogTrainingHttpResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedHttpResponse<CatalogTrainingHttpResponse>>> SearchTrainingsAsync(
        [FromQuery] CatalogSearchHttpRequest? search,
        [FromQuery] PaginationHttpRequest? pagination,
        CancellationToken cancellationToken = default)
    {
        var page = await queryDispatcher.DispatchAsync(search.ToQuery(pagination), cancellationToken);

        return Ok(page.ToHttp(trainings => trainings.ToHttp()));
    }

    /// <summary>
    /// Lists the catalog's facets: each topic at least one offered training declares, with its
    /// count.
    /// </summary>
    /// <remarks>
    /// The browse half of the search above (ADR 0069). Counted over the same composed visibility
    /// the search reads, so a suspension or a withholding moves these numbers the moment its
    /// consumer runs — a facet never promises a shelf the search would answer empty.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 200 OK with the facets, alphabetically by topic; empty when nothing is on offer.
    /// </returns>
    [HttpGet("topics")]
    [ProducesResponseType(typeof(CatalogTopicsHttpResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CatalogTopicsHttpResponse>> GetTopicsAsync(
        CancellationToken cancellationToken = default)
    {
        var facets = await queryDispatcher.DispatchAsync(
            HttpToApplicationMappings.ToGetCatalogTopicsQuery(), cancellationToken);

        return Ok(facets.ToHttp());
    }

    /// <summary>
    /// Reads one offered training in full, for a visitor who followed a search result.
    /// </summary>
    /// <remarks>
    /// The 404 is the same answer for a training nobody ever created and for one a moderator has
    /// withheld, deliberately: distinguishing the two would tell an anonymous caller that a
    /// training exists and has been taken down, which is the administration's read (ADR 0055).
    /// <para>
    /// No <c>ETag</c> here, unlike <c>GET /Training/{id}</c>. That one publishes a version because
    /// its caller comes back with <c>If-Match</c> to edit; a visitor has nothing to send back.
    /// </para>
    /// </remarks>
    /// <param name="trainingId">The training the route names.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 200 OK with the training as a visitor reads it.
    /// 400 Bad Request when the identifier names nothing.
    /// 404 Not Found when there is no offered training with this identifier.
    /// </returns>
    [HttpGet("trainings/{trainingId:guid}")]
    [ProducesResponseType(typeof(CatalogTrainingDetailHttpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CatalogTrainingDetailHttpResponse>> GetOfferedTrainingAsync(
        [NotEmptyIdentifier] Guid trainingId,
        CancellationToken cancellationToken = default)
    {
        var offered = await queryDispatcher.DispatchAsync(
            HttpToApplicationMappings.ToGetOfferedTrainingQuery(trainingId), cancellationToken);

        return offered is null ? NotFound() : Ok(offered.ToHttp());
    }

    /// <summary>
    /// Serves the portrait of the trainer behind an offered training.
    /// </summary>
    /// <remarks>
    /// The address names the training and the photo: a visitor on a training's page asks with what
    /// that page has in hand, exactly as the profile's portrait below asks with the trainer the
    /// profile names (ADR 0070). Naming the photo is what makes the response cacheable forever: a
    /// replacement mints a new photo identity, so these bytes never change.
    /// <para>
    /// One 404 for four situations, and the reason is the one the detail gives: no such training,
    /// none on offer, a photo that is not the owner's current one, and — the precondition ADR 0062
    /// named — a portrait carrying no proof that anything was ever stripped from it (ADR 0063).
    /// </para>
    /// </remarks>
    /// <param name="trainingId">The offered training the route names.</param>
    /// <param name="photoId">The photo the route names.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 200 OK with the image.
    /// 304 Not Modified when the caller already holds these bytes.
    /// 400 Bad Request when either identifier names nothing.
    /// 404 Not Found when there is no portrait a visitor may see at this address.
    /// </returns>
    [HttpGet("trainings/{trainingId:guid}/photo/{photoId:guid}")]
    // byte[] rather than a FileResult, for the reason GET /Trainer/{id}/photo gives at length: it is
    // what the document generator renders as a binary body, and naming the result type instead had
    // the generated client offering to deserialize a portrait as JSON.
    [Produces(TrainerPhoto.PngContentType, TrainerPhoto.JpegContentType, TrainerPhoto.WebpContentType)]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetOfferedPortraitAsync(
        [NotEmptyIdentifier] Guid trainingId,
        [NotEmptyIdentifier] Guid photoId,
        CancellationToken cancellationToken = default)
    {
        var portrait = await queryDispatcher.DispatchAsync(
            HttpToApplicationMappings.ToGetOfferedPortraitQuery(trainingId, photoId), cancellationToken);

        return portrait is null ? NotFound() : this.ImmutablePhotoFile(portrait);
    }

    /// <summary>
    /// Reads one offering trainer's public profile, for a visitor who followed a training.
    /// </summary>
    /// <remarks>
    /// Offered or invisible: the profile answers if and only if the index holds at least one entry
    /// for this trainer, so the 404 is the same for a person nobody ever registered, a suspended
    /// one, and one with nothing published (ADR 0070). Distinguishing them would tell an anonymous
    /// caller what only the administration may read (ADR 0055).
    /// </remarks>
    /// <param name="trainerId">The trainer the route names.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 200 OK with the trainer's public page: who they are, and what they offer.
    /// 400 Bad Request when the identifier names nothing.
    /// 404 Not Found when there is no offering trainer with this identifier.
    /// </returns>
    [HttpGet("trainers/{trainerId:guid}")]
    [ProducesResponseType(typeof(CatalogTrainerHttpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CatalogTrainerHttpResponse>> GetTrainerProfileAsync(
        [NotEmptyIdentifier] Guid trainerId,
        CancellationToken cancellationToken = default)
    {
        var profile = await queryDispatcher.DispatchAsync(
            HttpToApplicationMappings.ToGetTrainerProfileQuery(trainerId), cancellationToken);

        return profile is null ? NotFound() : Ok(profile.ToHttp());
    }

    /// <summary>
    /// Serves the portrait of an offering trainer.
    /// </summary>
    /// <remarks>
    /// The profile's own address for the same bytes its neighbor serves through a training: each
    /// page asks with what it has in hand (ADR 0070). Naming the photo is what makes the response
    /// cacheable forever — a replacement mints a new photo identity, so these bytes never change.
    /// <para>
    /// One 404 for four situations, and the reason is the one the profile gives: no offering
    /// trainer at this identifier, a photo that is not their current one, and — the precondition
    /// ADR 0062 named — a portrait carrying no proof that anything was ever stripped from it
    /// (ADR 0063).
    /// </para>
    /// </remarks>
    /// <param name="trainerId">The offering trainer the route names.</param>
    /// <param name="photoId">The photo the route names.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// 200 OK with the image.
    /// 304 Not Modified when the caller already holds these bytes.
    /// 400 Bad Request when either identifier names nothing.
    /// 404 Not Found when there is no portrait a visitor may see at this address.
    /// </returns>
    [HttpGet("trainers/{trainerId:guid}/photo/{photoId:guid}")]
    // byte[] rather than a FileResult, for the reason GET /Trainer/{id}/photo gives at length: it is
    // what the document generator renders as a binary body, and naming the result type instead had
    // the generated client offering to deserialize a portrait as JSON.
    [Produces(TrainerPhoto.PngContentType, TrainerPhoto.JpegContentType, TrainerPhoto.WebpContentType)]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetTrainerPortraitAsync(
        [NotEmptyIdentifier] Guid trainerId,
        [NotEmptyIdentifier] Guid photoId,
        CancellationToken cancellationToken = default)
    {
        var portrait = await queryDispatcher.DispatchAsync(
            HttpToApplicationMappings.ToGetTrainerPortraitQuery(trainerId, photoId), cancellationToken);

        return portrait is null ? NotFound() : this.ImmutablePhotoFile(portrait);
    }
}
