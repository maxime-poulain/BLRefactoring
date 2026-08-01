namespace BLRefactoring.DDDWithCqrs.Application.Pagination;

/// <summary>
/// One page of a read model, with what a caller needs to ask for the next.
/// </summary>
/// <typeparam name="T">The read model being paged.</typeparam>
/// <remarks>
/// It lives on the query side and nowhere else. The repositories deal in aggregates, and no use
/// case in this domain loads aggregates by the page: adding paging to their signatures would grow
/// the domain's surface for the sole benefit of a screen. Reads that feed a screen are exactly
/// what the query side is for, so that is where paging belongs — and being able to move it there
/// without touching the write model is the point of separating the two.
/// </remarks>
/// <param name="Items">The page itself.</param>
/// <param name="Page">The page returned, counted from 1.</param>
/// <param name="PageSize">How many items a full page holds.</param>
/// <param name="TotalCount">How many items match the query, all pages together.</param>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    /// <summary>
    /// How many pages the result spans, at least one even when nothing matched.
    /// </summary>
    /// <remarks>
    /// Derived rather than stored: two numbers that must agree are two numbers that can disagree.
    /// A caller asking for the first page of an empty set is on page 1 of 1, not page 1 of 0.
    /// </remarks>
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>
    /// Whether another page follows this one.
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Whether a page precedes this one.
    /// </summary>
    public bool HasPreviousPage => Page > 1;
}
