namespace TrainingHub.Shared.Common.Pagination;

/// <summary>
/// One page of a collection, with what a caller needs to ask for the next.
/// </summary>
/// <typeparam name="T">What the page holds.</typeparam>
/// <remarks>
/// Both stacks answer with it, filled in the way each reads: the CQRS query handlers project rows
/// straight into a page of DTOs, while the layered stack's repository answers a page of aggregates
/// that the application service then maps. It used to live on the query side alone, when only that
/// host paged; the page itself was never a query-side concept, only the projection that fills it,
/// so harmonizing the two hosts moved the envelope here and left the two ways of filling it where
/// they were.
/// </remarks>
/// <param name="Items">The page itself.</param>
/// <param name="Page">The page returned, counted from 1.</param>
/// <param name="PageSize">How many items a full page holds.</param>
/// <param name="TotalCount">How many items match, all pages together.</param>
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

    /// <summary>
    /// The same page, each item mapped — the counts and the position travel unchanged.
    /// </summary>
    /// <remarks>
    /// What the layered stack needs between its repository and its caller: the repository answers
    /// a page of aggregates, the application service owes a page of DTOs, and the metadata must
    /// cross that mapping untouched — rebuilding the envelope by hand at every call site is how
    /// a count and its items end up describing different sets.
    /// </remarks>
    /// <typeparam name="TResult">What each item becomes.</typeparam>
    /// <param name="projection">How one item becomes the other.</param>
    public PagedResult<TResult> Map<TResult>(Func<T, TResult> projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new PagedResult<TResult>([.. Items.Select(projection)], Page, PageSize, TotalCount);
    }
}
