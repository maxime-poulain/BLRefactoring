using TrainingHub.DDD.Application.Services.CatalogServices;
using TrainingHub.Shared.Api.Contracts;
using TrainingHub.Shared.Api.Contracts.Catalog;
using TrainingHub.Shared.Api.Contracts.Mappings;
using TrainingHub.Shared.Api.Contracts.Pagination;
using TrainingHub.Shared.Api.Controllers;
using TrainingHub.Shared.Api.Http;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace TrainingHub.DDD.Api.Controller;

/// <summary>
/// The public catalog, on the layered stack.
/// </summary>
/// <remarks>
/// The only controller in this API that anybody may call. What decides what it may answer is the
/// search index rather than the trainings table, which is what makes an anonymous read defensible
/// at all: the index holds what a visitor may be shown and nothing else (ADR 0059).
/// <para>
/// Three actions, and they read different places for the same reason. The search answers from the
/// index alone, because a list of titles is all the index holds. The detail takes its
/// <em>visibility</em> from the index and its <em>content</em> from the write model, because a
/// description copied into an index is a description that goes stale (ADR 0062). The portrait is the
/// detail's shape applied to bytes, and adds one condition of its own: what was never stripped is
/// never published (ADR 0063).
/// </para>
/// </remarks>
public sealed class CatalogController(ICatalogApplicationService catalogApplicationService)
    : CatalogControllerBase
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
    /// 400 Bad Request when the term is too long or the page is out of range.
    /// </returns>
    [HttpGet("trainings")]
    [ProducesResponseType(typeof(PagedHttpResponse<CatalogTrainingHttpResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedHttpResponse<CatalogTrainingHttpResponse>>> SearchTrainingsAsync(
        [FromQuery] CatalogSearchHttpRequest? search,
        [FromQuery] PaginationHttpRequest? pagination,
        CancellationToken cancellationToken = default)
    {
        var page = await catalogApplicationService.SearchAsync(
            search?.Term, pagination.ToPageRequest(), cancellationToken);

        return Ok(page.ToHttp(trainings => trainings.ToHttp()));
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
        var offered = await catalogApplicationService.FindOfferedAsync(trainingId, cancellationToken);

        return offered is null ? NotFound() : Ok(offered.ToHttp());
    }

    /// <summary>
    /// Serves the portrait of the trainer behind an offered training.
    /// </summary>
    /// <remarks>
    /// The address names the training and the photo, and no person. That is what lets a visitor be
    /// given it — they followed a catalog entry, and a trainer's identifier is not something a
    /// catalog hands out — and it is also what makes the response cacheable forever: a replacement
    /// mints a new photo identity, so these bytes never change.
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
        var portrait = await catalogApplicationService.FindOfferedPortraitAsync(
            trainingId, photoId, cancellationToken);

        return portrait is null ? NotFound() : this.ImmutablePhotoFile(portrait);
    }
}
