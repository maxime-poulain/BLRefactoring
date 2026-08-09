using TrainingHub.Shared.Api.Contracts;
using TrainingHub.Shared.Api.Contracts.Catalogue;
using TrainingHub.Shared.Api.Contracts.Mappings;
using TrainingHub.Shared.Api.Contracts.Pagination;
using TrainingHub.Shared.Api.Controllers;
using TrainingHub.DDDWithCqrs.Api.Mappings;
using TrainingHub.Shared.CQS;
using Microsoft.AspNetCore.Mvc;

namespace TrainingHub.DDDWithCqrs.Api.Controller;

/// <summary>
/// The public catalogue, on the CQRS stack.
/// </summary>
/// <remarks>
/// The only controller in this API that anybody may call. What decides what it may answer is the
/// search index rather than the trainings table, which is what makes an anonymous read defensible
/// at all: the index holds what a visitor may be shown and nothing else (ADR 0059).
/// <para>
/// Two actions since ADR 0062, and they read different places for the same reason. The search
/// answers from the index alone, because a list of titles is all the index holds. The detail takes
/// its <em>visibility</em> from the index and its <em>content</em> from the write model, because a
/// description copied into an index is a description that goes stale.
/// </para>
/// </remarks>
public sealed class CatalogueController(IQueryDispatcher queryDispatcher) : CatalogueControllerBase
{
    /// <summary>
    /// Searches the offered catalogue by title, or lists it when no term is given.
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
    [ProducesResponseType(typeof(PagedHttpResponse<CatalogueTrainingHttpResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedHttpResponse<CatalogueTrainingHttpResponse>>> SearchTrainingsAsync(
        [FromQuery] CatalogueSearchHttpRequest? search,
        [FromQuery] PaginationHttpRequest? pagination,
        CancellationToken cancellationToken = default)
    {
        var page = await queryDispatcher.DispatchAsync(search.ToQuery(pagination), cancellationToken);

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
    [ProducesResponseType(typeof(CatalogueTrainingDetailHttpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CatalogueTrainingDetailHttpResponse>> GetOfferedTrainingAsync(
        [NotEmptyIdentifier] Guid trainingId,
        CancellationToken cancellationToken = default)
    {
        var offered = await queryDispatcher.DispatchAsync(
            HttpToApplicationMappings.ToGetOfferedTrainingQuery(trainingId), cancellationToken);

        return offered is null ? NotFound() : Ok(offered.ToHttp());
    }
}
