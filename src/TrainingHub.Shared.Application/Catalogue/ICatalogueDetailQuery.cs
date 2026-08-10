using TrainingHub.Shared.Application.Dtos.Trainer;
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

    /// <summary>
    /// The portrait of the trainer who owns this offered training, or <see langword="null"/> when
    /// there is none a visitor may see.
    /// </summary>
    /// <remarks>
    /// The same sharing of authority as its neighbour, applied to bytes: the index says whether the
    /// training is on offer, and the write model says which photo its owner has. Reached through the
    /// training rather than through the trainer on purpose — what a visitor followed is a catalogue
    /// entry, and no identifier of a person belongs in a public address (ADR 0063).
    /// <para>
    /// Four ways to answer nothing, and the action turns all four into the same 404: no such
    /// training, not on offer, a photo identity that is not the one the owner currently has, and a
    /// photo carrying no sanitisation stamp. The last is the precondition ADR 0062 named — what was
    /// never stripped is never published, and a portrait stored before that record can prove
    /// nothing about itself.
    /// </para>
    /// </remarks>
    /// <param name="trainingId">The offered training the visitor is looking at.</param>
    /// <param name="photoId">The photo its address names.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<TrainerPhotoDto?> FindOfferedPortraitAsync(
        Guid trainingId,
        Guid photoId,
        CancellationToken cancellationToken = default);
}
