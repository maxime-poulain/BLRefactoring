using System.ComponentModel.DataAnnotations;

namespace TrainingHub.Shared.Api.Contracts.Catalog;

/// <summary>
/// What the catalog's facets are counted under, bound from the query string.
/// </summary>
/// <remarks>
/// One property, and the three it does not have are the point. The facets answer the term because
/// a count that ignored it would advertise shelves the current search cannot reach; they ignore the
/// shelves because under a widening filter (ADR 0080) counting each topic against the ticked ones
/// would give every facet the size of the whole selection; and an order does not apply to a set of
/// counts that is always alphabetical.
/// <para>
/// A type of its own rather than <see cref="CatalogSearchHttpRequest"/> bound a second time. Both
/// would have worked and one would have let a caller send the same query string to either endpoint
/// — but the generated client is the reason: reusing the search's request puts a
/// <c>topic</c> and a <c>sort</c> parameter on <c>GetTopicsAsync</c> that do nothing, and a
/// published surface whose parameters are ignored is one every caller has to be told about.
/// </para>
/// </remarks>
public sealed class CatalogFacetsHttpRequest
{
    /// <summary>
    /// Only the shelves a training matching every word of this declares, or all of them when it is
    /// absent.
    /// </summary>
    /// <remarks>
    /// Bounded exactly as the search's term is, and for the same reason: a term longer than any
    /// title can match nothing, so refusing it says more than answering an empty set would. The two
    /// endpoints refuse the same term, which is what lets a screen send one word to both without
    /// discovering that only one of them minds.
    /// </remarks>
    [StringLength(100)]
    public string? Term { get; init; }
}
