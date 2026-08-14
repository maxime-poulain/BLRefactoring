using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using TrainingHub.Shared.Application.Search;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

namespace TrainingHub.Shared.Api.Contracts.Catalog;

/// <summary>
/// What narrows <c>GET /Catalog/trainings</c>, bound from the query string.
/// </summary>
/// <remarks>
/// Two criteria and an order. The term is the one ADR 0055 could not offer: something to look for
/// in a training's title. There is no status here — an entry the catalog serves is on offer by
/// construction, and the states that are not are the states the index does not hold (ADR 0059).
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
    /// Only trainings filed under at least one of these topics, or every topic when none is given.
    /// </summary>
    /// <remarks>
    /// Names from the domain's closed set, refused at model binding when any of them is anything
    /// else — <c>[KnownTopic]</c> asks the domain rather than restating its list, and judges a
    /// sequence name by name. Composable with the term: a visitor can browse several shelves and
    /// search along them in the same breath (ADR 0069, ADR 0080).
    /// <para>
    /// <b>The wire name stays singular.</b> The property is plural because it holds several, but
    /// the parameter is still <c>topic</c>, repeated — <c>?topic=Design&amp;topic=Programming</c>.
    /// That is what keeps every address already shared, bookmarked or indexed answering exactly
    /// what it answered before: a lone <c>?topic=Design</c> binds to a sequence of one, and neither
    /// a redirect nor a second spelling has to exist.
    /// </para>
    /// <para>
    /// <b>No length bound and no count bound, and both absences are decisions.</b>
    /// <c>[StringLength]</c> is gone because it casts what it is given to a string and would throw
    /// on a sequence — and because it was never the thing doing the work: a name outside the closed
    /// set is refused by identity, which subsumes any length. Nothing caps how many may arrive
    /// either, because nothing needs to: unknown names are refused, the known ones are sixteen, and
    /// the layer below deduplicates — so a caller who sends a thousand is asking a question with at
    /// most sixteen distinct parts, under the collection size the framework already bounds binding
    /// by.
    /// </para>
    /// </remarks>
    [FromQuery(Name = "topic")]
    [KnownTopic(typeof(Topic))]
    public string[]? Topics { get; init; }

    /// <summary>
    /// Which of the catalog's published orders to read the page in, or the default — the newest
    /// first — when it is absent.
    /// </summary>
    /// <remarks>
    /// A name from a closed set, refused at model binding when it is anything else —
    /// <c>[KnownSort]</c> asks <see cref="CatalogOrder"/> rather than restating its members, so
    /// the contract and the application publish the same two names (ADR 0071). Case does not
    /// matter: the name is translated to an enum member at the boundary and stored nowhere.
    /// </remarks>
    [KnownSort(typeof(CatalogOrder))]
    [StringLength(20)]
    public string? Sort { get; init; }
}
