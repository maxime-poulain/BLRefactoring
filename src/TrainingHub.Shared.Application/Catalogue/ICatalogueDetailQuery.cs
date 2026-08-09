using TrainingHub.Shared.Application.Dtos.Training;

namespace TrainingHub.Shared.Application.Catalogue;

/// <summary>
/// Reads one offered training in full, for a visitor who followed a search result (ADR 0062).
/// </summary>
/// <remarks>
/// A port of its own rather than a method on <c>ITrainingSearchQuery</c>, because it asks a
/// different question of a different place. ADR 0059 gave the Search Indexing context a query
/// surface and a rule that keeps a <em>search</em> off the aggregates — <em>"the index is the
/// answer or there is no index"</em> — and that rule is about searching. Reading one training by
/// identifier is not a search, and the index has never held anything to read.
/// <para>
/// So the answer is assembled from two authorities, and which one owns which half is the decision
/// this port exists to hold:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Visibility comes from the index.</b> An entry exists if and only if the training is on offer —
/// published, its owner in good standing, not withheld — because that is what the nine consumers of
/// ADR 0056 compose into it. Asking the write model the same question a second way is how two
/// definitions of "on offer" come to disagree.
/// </description></item>
/// <item><description>
/// <b>Content comes from the write model.</b> The index holds a title and nothing else, and adding
/// a description to it would be storing a copy that goes stale the moment the training is edited —
/// where a live read cannot. The trainer's name arrives the same way, and for the sharper version
/// of the same reason: no integration event carries a rename at all.
/// </description></item>
/// </list>
/// </remarks>
public interface ICatalogueDetailQuery
{
    /// <summary>
    /// The offered training with this identifier, or <see langword="null"/> when there is none a
    /// visitor may see.
    /// </summary>
    /// <remarks>
    /// One answer for "no such training" and for "not on offer", deliberately: telling a visitor
    /// that a training exists but has been taken down is the administration's read, not theirs
    /// (ADR 0055).
    /// </remarks>
    /// <param name="trainingId">The training the visitor asked for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<CatalogueTrainingDetailDto?> FindOfferedAsync(
        Guid trainingId,
        CancellationToken cancellationToken = default);
}
