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
