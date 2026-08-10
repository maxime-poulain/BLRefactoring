using TrainingHub.DDDWithCqrs.Application.Features.Catalog.GetTopics;
using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Application.Search;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Infrastructure.Features.Catalog.GetTopics;

/// <summary>
/// Answers the catalog's facets by asking the search index, and nothing else.
/// </summary>
/// <remarks>
/// The same seam as its search sibling, for the same reason: the index is another context's read
/// model, published behind <see cref="ITrainingSearchQuery"/>, and reaching past that language
/// into its tables would make the two contexts one again (ADR 0023, ADR 0059, ADR 0069).
/// </remarks>
public sealed class GetCatalogTopicsQueryHandler(ITrainingSearchQuery trainingSearch)
    : IQueryHandler<GetCatalogTopicsQuery, IReadOnlyList<TopicFacetDto>>
{
    /// <summary>
    /// Runs the query.
    /// </summary>
    public async ValueTask<IReadOnlyList<TopicFacetDto>> Handle(
        GetCatalogTopicsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await trainingSearch.FacetsAsync(cancellationToken);
    }
}
