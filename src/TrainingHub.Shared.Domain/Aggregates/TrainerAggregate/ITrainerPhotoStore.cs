using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Storage;

namespace TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;

/// <summary>
/// Keeps the bytes of a trainer's photo, wherever those end up living.
/// </summary>
/// <remarks>
/// The thin layer between the aggregate and <see cref="IObjectStore"/>: this one speaks of
/// trainers and photos, the store underneath speaks of keys and bytes, and the translation between
/// the two — the key layout — belongs to neither the domain nor the use cases. It sits beside
/// <c>ITrainerRepository</c> for the same reason that one does: the aggregate states what it needs
/// kept, and the infrastructure decides how.
/// </remarks>
public interface ITrainerPhotoStore
{
    /// <summary>
    /// Stores the bytes of a photo.
    /// </summary>
    /// <param name="trainerId">The trainer the photo belongs to.</param>
    /// <param name="photo">The photo being stored.</param>
    /// <param name="content">Its bytes.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the bytes are stored.</returns>
    Task StoreAsync(
        TrainerId trainerId,
        TrainerPhoto photo,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the bytes of a photo.
    /// </summary>
    /// <param name="trainerId">The trainer the photo belongs to.</param>
    /// <param name="photoId">Which photo of theirs to fetch.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes, or <see langword="null"/> when they are not there.</returns>
    /// <remarks>
    /// The two fields the key is made of, rather than the whole value object its neighbors take.
    /// A read is the one operation that has nothing else to say: <see cref="StoreAsync"/> needs the
    /// media type it is about to record, and <see cref="DeleteAsync"/> is always called by something
    /// that has just taken the photo off an aggregate — but a reader may have no aggregate at all.
    /// The catalog's portrait is exactly that: a route names a trainer and a photo, the read side
    /// projects columns, and materializing a value object to compute a key out of two of them would
    /// be work done to satisfy a signature (ADR 0063).
    /// </remarks>
    Task<StoredObject?> FetchAsync(
        TrainerId trainerId,
        PhotoId photoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the bytes of a photo.
    /// </summary>
    /// <param name="trainerId">The trainer the photo belonged to.</param>
    /// <param name="photoId">Which photo of theirs to delete.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>A task that completes when the bytes are gone.</returns>
    /// <remarks>
    /// The same two fields as <see cref="FetchAsync"/>, for the reason that used to separate
    /// them: a deleter was always something that had just taken the photo off an aggregate,
    /// until the erasure's collector — which runs after the aggregate's rows are gone and knows
    /// the photo only by the identity the fact carried (ADR 0085). A deleter, like a reader, may
    /// have no aggregate at all.
    /// </remarks>
    Task DeleteAsync(
        TrainerId trainerId,
        PhotoId photoId,
        CancellationToken cancellationToken = default);
}
