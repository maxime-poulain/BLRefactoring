using TrainingHub.Shared.Application.Search;
using TrainingHub.Shared.Common.Pagination;

namespace TrainingHub.DDD.Application.Services.CatalogServices;

/// <summary>
/// What the layered stack's catalog search takes: the question, whole.
/// </summary>
/// <remarks>
/// The layered mirror of <c>SearchCatalogQuery</c>, carrying the same four members for the same
/// reasons — and shaped as one request model rather than a parameter list because the search now
/// carries an application-owned type, <see cref="CatalogOrder"/>, and a layered signature says
/// which boundary a type is on by its suffix (ADR 0048). What a controller hands an application
/// service is a <c>*Request</c>; the mapping from the HTTP contract happens before this exists.
/// </remarks>
public sealed class CatalogSearchRequest
{
    /// <summary>
    /// What to look for, or <see langword="null"/> for the whole offered catalog.
    /// </summary>
    public string? Term { get; init; }

    /// <summary>
    /// The canonical name of a topic to browse, or <see langword="null"/> for all of them
    /// (ADR 0069).
    /// </summary>
    public string? Topic { get; init; }

    /// <summary>
    /// Which of the catalog's two published orders to read the page in (ADR 0071).
    /// </summary>
    public CatalogOrder Order { get; init; }

    /// <summary>
    /// The page asked for; the default page when none was.
    /// </summary>
    public PageRequest Paging { get; init; } = new();
}
