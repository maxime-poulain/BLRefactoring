using System.ComponentModel.DataAnnotations;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

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

    /// <summary>
    /// Only trainings filed under this topic, or every topic when it is absent.
    /// </summary>
    /// <remarks>
    /// A name from the domain's closed set, refused at model binding when it is anything else —
    /// <c>[KnownTopic]</c> asks the domain rather than restating its list. Composable with the
    /// term: a visitor can browse a shelf and search along it in the same breath (ADR 0069).
    /// </remarks>
    [KnownTopic(typeof(Topic))]
    [StringLength(50)]
    public string? Topic { get; init; }
}
