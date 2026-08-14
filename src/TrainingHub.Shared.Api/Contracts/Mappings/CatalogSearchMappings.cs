using TrainingHub.Shared.Api.Contracts.Catalog;
using TrainingHub.Shared.Application.Search;

namespace TrainingHub.Shared.Api.Contracts.Mappings;

/// <summary>
/// Translates the catalog search contract's order, for both hosts.
/// </summary>
/// <remarks>
/// One file for the same reason <see cref="PaginationMappings"/> is one file: the sort name maps to
/// the same inner type on both sides, neither host has anything to add, and two copies of the
/// translation would be two places for the default to drift (ADR 0029, ADR 0071).
/// </remarks>
public static class CatalogSearchMappings
{
    /// <summary>
    /// The shelves a caller asked for, normalized: blanks dropped, duplicates collapsed, order
    /// irrelevant.
    /// </summary>
    /// <remarks>
    /// Written once and called by both hosts, where the single-topic reading of the same idea was
    /// copied into two places — the layered controller's body and the CQRS mapping — and would have
    /// had to be kept in step by hand. ADR 0049's detector exists to see exactly that.
    /// <para>
    /// Blank is what an empty query parameter binds to, and it asks for no filter rather than for a
    /// topic called nothing: <c>?topic=Design&amp;topic=</c> is one shelf, not one shelf and a
    /// mistake. Duplicates collapse because under ADR 0080 they cannot mean anything — a shelf
    /// asked for twice widens the answer no further than asking once — and collapsing them here is
    /// what bounds the question at the sixteen the domain declares, however many the caller sent.
    /// </para>
    /// </remarks>
    /// <param name="search">What the caller bound, possibly nothing at all.</param>
    /// <returns>The distinct, non-blank names; empty when no shelf was asked for.</returns>
    public static IReadOnlyCollection<string> ToTopics(this CatalogSearchHttpRequest? search) =>
        search?.Topics is not { Length: > 0 } topics
            ? []
            : [.. topics
                .Where(topic => !string.IsNullOrWhiteSpace(topic))
                .Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// Reads the order a caller asked for, or the default — newest first — when they asked for
    /// none.
    /// </summary>
    /// <remarks>
    /// The parse cannot fail for what the boundary admits: <c>[KnownSort]</c> has already refused
    /// any name <see cref="CatalogOrder"/> does not declare, and a blank binds like an absence.
    /// The fallback to the default is therefore the absence case, not a swallowed error. The
    /// default is the newest order because the bare catalog is the front door: what a visitor
    /// with no question sees first is what recently went on offer, and the alphabet is one click
    /// away for whoever wants to scan it (ADR 0074).
    /// </remarks>
    public static CatalogOrder ToOrder(this CatalogSearchHttpRequest? search) =>
        Enum.TryParse<CatalogOrder>(search?.Sort, ignoreCase: true, out var order)
            ? order
            : CatalogOrder.Newest;
}
