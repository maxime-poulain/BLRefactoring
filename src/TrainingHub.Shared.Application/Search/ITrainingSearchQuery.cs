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
/// The port takes a term and page coordinates and nothing else. Not a predicate, not an ordering:
/// the same line ADR 0055 drew on the repositories' named questions, drawn again on a read model,
/// and for the same reason — a caller that composed the query would be writing SQL for a schema it
/// cannot see.
/// </para>
/// </remarks>
public interface ITrainingSearchQuery
{
    /// <summary>
    /// The page of offered trainings whose titles match every word of the term.
    /// </summary>
    /// <param name="term">
    /// What to look for. A blank term is no term at all, and answers the offered catalog — the
    /// same reading the trainers' listing gives it (ADR 0055).
    /// </param>
    /// <param name="paging">The page asked for, under the published cap (ADR 0029).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The page, in the total order the index is stored in, and the count of everything that
    /// matched — never a page filtered after it was read.
    /// </returns>
    Task<PagedResult<CatalogTrainingDto>> SearchAsync(
        string? term,
        PageRequest paging,
        CancellationToken cancellationToken = default);
}
