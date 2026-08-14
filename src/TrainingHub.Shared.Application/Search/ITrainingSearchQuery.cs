using TrainingHub.Shared.Application.Dtos.Training;
using TrainingHub.Shared.Common.Pagination;

namespace TrainingHub.Shared.Application.Search;

/// <summary>
/// Reads the search index: the query half of the language this context publishes.
/// </summary>
/// <remarks>
/// The surface ADR 0055 refused to open before there was an index behind it — <em>"giving it a
/// query surface before it has an index is building the second system to avoid a `LIKE`"</em> — and
/// which ADR 0059 opens now that there is one.
/// <para>
/// It is the only way anything reads the index, and it answers only what a visitor may see: the
/// entries the write model's two aggregates compose into "on offer" (ADR 0050, ADR 0056). That is
/// also why no administrative listing can be served from here — a moderator looks for exactly what
/// this refuses to return.
/// </para>
/// <para>
/// The port takes a term, the shelves to look on, page coordinates and one of two named orders,
/// and nothing else. Not a predicate, not a sort expression: the same line ADR 0055 drew on the
/// repositories' named questions, drawn again on a read model, and for the same reason — a caller
/// that composed the query would be writing SQL for a schema it cannot see. An order is chosen
/// from <see cref="CatalogOrder"/>'s closed set, never described (ADR 0071).
/// </para>
/// <para>
/// The shelves are several rather than one, and they widen rather than narrow (ADR 0080). That is
/// the opposite of what the term's words do to each other, and the difference is deliberate: a
/// word the visitor adds says more about what they are looking for, while a shelf they tick says
/// they would also accept what is on it.
/// </para>
/// </remarks>
public interface ITrainingSearchQuery
{
    /// <summary>
    /// The page of offered trainings whose titles match every word of the term, filed under any of
    /// the topics asked for.
    /// </summary>
    /// <param name="term">
    /// What to look for. A blank term is no term at all, and answers the offered catalog — the
    /// same reading the trainers' listing gives it (ADR 0055).
    /// </param>
    /// <param name="topics">
    /// The canonical names of the topics to browse. Empty is every topic rather than none: a
    /// visitor who has ticked nothing is asking for the whole catalog, not for an empty one. A
    /// training answers when it carries <em>any</em> of them (ADR 0080). The boundary and the
    /// validators refuse a name the domain does not spell, so what arrives here is <c>Topic</c>'s
    /// own form — a name that is not matches nothing, which is the honest answer to a question no
    /// shelf carries (ADR 0069).
    /// </param>
    /// <param name="order">
    /// Which of the two named orders to read the page in (ADR 0071). Both are total, so either
    /// pages without a row appearing twice or never.
    /// </param>
    /// <param name="paging">The page asked for, under the published cap (ADR 0029).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The page, in the order asked for, and the count of everything that matched — never a page
    /// filtered after it was read.
    /// </returns>
    Task<PagedResult<CatalogTrainingDto>> SearchAsync(
        string? term,
        IReadOnlyCollection<string> topics,
        CatalogOrder order,
        PageRequest paging,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The facets of the offered catalog under a term: each topic at least one matching training
    /// declares, with its count.
    /// </summary>
    /// <remarks>
    /// Counted over the same composed visibility the search reads, so a suspension or a
    /// withholding moves these numbers the moment its consumer runs — a facet never promises a
    /// shelf the search would answer empty (ADR 0069).
    /// <para>
    /// It takes the term because a count that ignored it would advertise shelves the current
    /// search cannot reach. It does <em>not</em> take the topics, and that is the arithmetic of
    /// widening rather than an omission: under ADR 0080 a ticked shelf adds to the answer, so
    /// counting each topic against the ticked ones would give every facet the same number — the
    /// size of the whole selection — and the figures would stop telling the shelves apart. What a
    /// visitor needs to read off a chip is what it would contribute to the search they have typed,
    /// which is what this counts.
    /// </para>
    /// </remarks>
    /// <param name="term">
    /// The term the facets are counted under, read exactly as <see cref="SearchAsync"/> reads it.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The facets, alphabetically by topic name; empty when nothing matches.</returns>
    Task<IReadOnlyList<TopicFacetDto>> FacetsAsync(
        string? term,
        CancellationToken cancellationToken = default);
}
