using TrainingHub.DDDWithCqrs.Application.Features.Catalogue.Search;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Application.Search;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Infrastructure.Features.Catalogue.Search;

/// <summary>
/// Answers the catalogue search by asking the search index the one question it answers.
/// </summary>
/// <remarks>
/// The only reader here that opens no <c>DbSet</c>, and the reason is what this repository means by
/// a bounded context. Its neighbours project columns out of the write model because that model is
/// this application's own; the index is not — it is a context with a published language of four
/// write operations and one read, and reaching past that language into its tables would make the
/// two contexts one again (ADR 0023, ADR 0059).
/// <para>
/// So both stacks arrive at <see cref="ITrainingSearchQuery"/>, and the pass-through is the honest
/// shape rather than a shortcut: what usually separates the two hosts is how each drives the write
/// model, and over a read model there is nothing to separate.
/// </para>
/// </remarks>
public sealed class SearchCatalogueQueryHandler(ITrainingSearchQuery trainingSearch)
    : IQueryHandler<SearchCatalogueQuery, PagedResult<CatalogueTrainingDto>>
{
    /// <summary>
    /// Runs the query.
    /// </summary>
    public async ValueTask<PagedResult<CatalogueTrainingDto>> Handle(
        SearchCatalogueQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await trainingSearch.SearchAsync(request.Term, request.Paging, cancellationToken);
    }
}
