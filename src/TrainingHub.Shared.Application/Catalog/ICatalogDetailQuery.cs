using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.Application.Dtos.Training;

namespace TrainingHub.Shared.Application.Catalog;

/// <summary>
/// Reads one offered training in full, or one offering trainer's profile, for a visitor who
/// followed a search result (ADR 0062, ADR 0070).
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
public interface ICatalogDetailQuery
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
    Task<CatalogTrainingDetailDto?> FindOfferedAsync(
        Guid trainingId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The portrait of the trainer who owns this offered training, or <see langword="null"/> when
    /// there is none a visitor may see.
    /// </summary>
    /// <remarks>
    /// The same sharing of authority as its neighbor, applied to bytes: the index says whether the
    /// training is on offer, and the write model says which photo its owner has. Reached through
    /// the training because that is what this visitor has in hand — a catalog entry names the page
    /// they are on, where the address by person below belongs to the profile (ADR 0070).
    /// <para>
    /// Four ways to answer nothing, and the action turns all four into the same 404: no such
    /// training, not on offer, a photo identity that is not the one the owner currently has, and a
    /// photo carrying no sanitization stamp. The last is the precondition ADR 0062 named — what was
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

    /// <summary>
    /// The public profile of an offering trainer, or <see langword="null"/> when there is none a
    /// visitor may see.
    /// </summary>
    /// <remarks>
    /// Offered or invisible, and nothing in between: a profile answers if and only if the index
    /// holds at least one entry for this trainer, so a person nobody ever registered, a suspended
    /// one, and one with nothing published are all the same <see langword="null"/> (ADR 0070). One
    /// predicate, and it is the index's — asking the write model who is presentable would be the
    /// second definition of "on offer" this port exists to prevent (ADR 0062).
    /// <para>
    /// The same sharing of authority as the detail: the index says whether the person is offering,
    /// and what they offer; the write model says who they are, read at the moment of the request
    /// because no integration event carries a rename.
    /// </para>
    /// </remarks>
    /// <param name="trainerId">The trainer the visitor asked for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<CatalogTrainerDto?> FindOfferedTrainerAsync(
        Guid trainerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The portrait of an offering trainer, or <see langword="null"/> when there is none a visitor
    /// may see.
    /// </summary>
    /// <remarks>
    /// The profile's own address for the same bytes its neighbor serves through a training. Two
    /// addresses on purpose rather than one redirected: each page asks for the portrait with what
    /// it has in hand, and both answers are cacheable forever because both name the photo
    /// (ADR 0070).
    /// <para>
    /// The same four refusals as the neighbor, one of them differently reached: no offering
    /// trainer at this identifier, a photo identity that is not the current one, no sanitization
    /// stamp, or bytes the store does not hold.
    /// </para>
    /// </remarks>
    /// <param name="trainerId">The offering trainer the visitor is looking at.</param>
    /// <param name="photoId">The photo its address names.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<TrainerPhotoDto?> FindTrainerPortraitAsync(
        Guid trainerId,
        Guid photoId,
        CancellationToken cancellationToken = default);
}
