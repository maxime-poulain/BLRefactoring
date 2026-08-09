using TrainingHub.DDD.Application.Services.CatalogueServices;
using TrainingHub.Shared.Api.Contracts.Catalogue;
using TrainingHub.Shared.Api.Contracts.Mappings;
using TrainingHub.Shared.Api.Contracts.Pagination;
using TrainingHub.Shared.Api.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace TrainingHub.DDD.Api.Controller;

/// <summary>
/// The public catalogue, on the layered stack.
/// </summary>
/// <remarks>
/// One action, and the only one in this API that anybody may call. What it reads is the search
/// index rather than the trainings table, which is what makes an anonymous read defensible at all:
/// the index holds what a visitor may be shown and nothing else (ADR 0059).
/// </remarks>
public sealed class CatalogueController(ICatalogueApplicationService catalogueApplicationService)
    : CatalogueControllerBase
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
        var page = await catalogueApplicationService.SearchAsync(
            search?.Term, pagination.ToPageRequest(), cancellationToken);

        return Ok(page.ToHttp(trainings => trainings.ToHttp()));
    }
}
