using System.ComponentModel.DataAnnotations;

namespace TrainingHub.Shared.Api.Contracts.Catalog;

/// <summary>
/// What narrows <c>GET /Catalog/trainings</c>, bound from the query string.
/// </summary>
/// <remarks>
/// One criterion, and it is the one ADR 0055 could not offer: a term to look for in a training's
/// title. There is no status here — an entry the catalog serves is on offer by construction, and
/// the states that are not are the states the index does not hold (ADR 0059).
/// <para>
/// Bound as a second <c>[FromQuery]</c> object beside <c>PaginationHttpRequest</c>, so the bounds
/// ADR 0029 published exist in one place, exactly as the administrative filters are.
/// </para>
/// </remarks>
public sealed class CatalogSearchHttpRequest
{
    /// <summary>
    /// Only trainings whose title matches every word of this, or the whole catalog when it is
    /// absent.
    /// </summary>
    /// <remarks>
    /// Bounded to the length of the column it is matched against — a term longer than any title
    /// can match nothing, so refusing it says more than answering an empty page would.
    /// <para>
    /// Unlike the trainers' term, this one is answered by an index rather than by a scan: the words
    /// of a title are stored as rows, and each word of the term is a prefix seek. Which is why this
    /// search exists here at all, and not on the administrative listing — ADR 0055 named an index as
    /// the condition, and ADR 0059 is where it was met.
    /// </para>
    /// </remarks>
    [StringLength(100)]
    public string? Term { get; init; }
}
