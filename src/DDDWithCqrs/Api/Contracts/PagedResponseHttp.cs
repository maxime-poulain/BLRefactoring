namespace BLRefactoring.DDDWithCqrs.Api.Contracts;

/// <summary>
/// One page of a collection, as the API publishes it.
/// </summary>
/// <typeparam name="T">The response contract being paged.</typeparam>
/// <remarks>
/// An envelope rather than a bare array, so the metadata a caller needs to build a pager travels
/// with the page instead of in headers a browser client would have to be granted access to read.
/// <para>
/// The counts are recomputed from the query result rather than forwarded blindly, which keeps this
/// type free of the application's <c>PagedResult</c> and lets either evolve on its own.
/// </para>
/// </remarks>
public sealed class PagedResponseHttp<T>
{
    /// <summary>The page itself.</summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>The page returned, counted from 1.</summary>
    public required int Page { get; init; }

    /// <summary>How many items a full page holds.</summary>
    public required int PageSize { get; init; }

    /// <summary>How many items match, all pages together.</summary>
    public required int TotalCount { get; init; }

    /// <summary>How many pages the result spans, at least one.</summary>
    public required int TotalPages { get; init; }

    /// <summary>Whether another page follows this one.</summary>
    public required bool HasNextPage { get; init; }

    /// <summary>Whether a page precedes this one.</summary>
    public required bool HasPreviousPage { get; init; }
}
