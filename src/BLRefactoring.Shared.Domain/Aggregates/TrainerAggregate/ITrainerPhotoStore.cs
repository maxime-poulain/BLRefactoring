using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using BLRefactoring.Shared.Storage;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;

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
    /// <param name="photo">The photo to fetch.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes, or <see langword="null"/> when they are not there.</returns>
    Task<StoredObject?> FetchAsync(
        TrainerId trainerId,
        TrainerPhoto photo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the bytes of a photo.
    /// </summary>
    /// <param name="trainerId">The trainer the photo belonged to.</param>
    /// <param name="photo">The photo to delete.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>A task that completes when the bytes are gone.</returns>
    Task DeleteAsync(
        TrainerId trainerId,
        TrainerPhoto photo,
        CancellationToken cancellationToken = default);
}
