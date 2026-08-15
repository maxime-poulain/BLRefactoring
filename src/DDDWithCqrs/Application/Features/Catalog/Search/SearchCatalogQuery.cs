using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Application.Search;
using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.CQS;

namespace TrainingHub.DDDWithCqrs.Application.Features.Catalog.Search;

/// <summary>
/// One page of the offered catalog, narrowed by a term when there is one (ADR 0059).
/// </summary>
/// <remarks>
/// The first query here that reads no aggregate table. Its neighbors ask the write model a
/// question and project columns out of it; this one asks the search index, which is a read model
/// maintained after the commit by nine consumers of the outbox (ADR 0025, ADR 0056).
/// <para>
/// Its sibling <c>GetTrainingsByStatusQuery</c> carries a term too since ADR 0060, and the two
/// remain different questions: this one seeks along an inverted index and serves anybody, that one
/// scans the write model for one authority and can find the states this index refuses to hold.
/// </para>
/// <para>
/// No status, and nothing that names a caller. What may be shown is composed when the index is
/// written rather than asked for when it is read, which is what makes an anonymous read of it safe.
/// </para>
/// </remarks>
public sealed class SearchCatalogQuery : IQuery<PagedResult<CatalogTrainingDto>>
{
    /// <summary>
    /// What to look for, or <see langword="null"/> for the whole offered catalog.
    /// </summary>
    public string? Term { get; init; }

    /// <summary>
    /// The canonical name of a topic to browse, or <see langword="null"/> for all of them
    /// (ADR 0069).
    /// </summary>
    public IReadOnlyCollection<string> Topics { get; init; } = [];

    /// <summary>
    /// Which of the catalog's two published orders to read the page in (ADR 0071).
    /// </summary>
    public CatalogOrder Order { get; init; }

    /// <summary>
    /// The page asked for; the default page when none was.
    /// </summary>
    public PageRequest Paging { get; init; } = new();
}
