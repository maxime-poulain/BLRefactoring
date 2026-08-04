using TrainingHub.Shared.Api.Contracts.Pagination;
using TrainingHub.Shared.Common.Pagination;

namespace TrainingHub.Shared.Api.Contracts.Mappings;

/// <summary>
/// Translates the paging contract both hosts serve, in both directions.
/// </summary>
/// <remarks>
/// One file rather than a method in each host's mappings, because unlike the other requests this
/// one maps to the same inner type on both sides: the layered host hands the <see cref="PageRequest"/>
/// to its application service, the CQRS host folds it into a query, and neither has anything
/// host-specific to add. Two copies of this translation would be two places for a default to drift.
/// </remarks>
public static class PaginationMappings
{
    /// <summary>
    /// Reads the page a caller asked for, or the default page when they asked for none.
    /// </summary>
    /// <remarks>
    /// The parameter is nullable because an absent <c>[FromQuery]</c> contract binds to
    /// <see langword="null"/> when the caller passes no query string at all — and an unpaged call
    /// must not mean an unbounded read, so the defaults apply here as well as on the contract.
    /// </remarks>
    public static PageRequest ToPageRequest(this PaginationRequestHttp? pagination)
        => new() { Page = pagination?.Page ?? 1, PageSize = pagination?.PageSize ?? PageRequest.DefaultPageSize };

    /// <summary>
    /// Publishes one page of read models, recomputing the metadata from the query result.
    /// </summary>
    public static PagedResponseHttp<TResponse> ToHttp<TItem, TResponse>(
        this PagedResult<TItem> page,
        Func<IEnumerable<TItem>, List<TResponse>> mapItems)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(mapItems);

        return new PagedResponseHttp<TResponse>
        {
            Items = mapItems(page.Items),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
            TotalPages = page.TotalPages,
            HasNextPage = page.HasNextPage,
            HasPreviousPage = page.HasPreviousPage
        };
    }
}
