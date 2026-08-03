using System.Globalization;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using BLRefactoring.Shared.Storage;

namespace BLRefactoring.Shared.Infrastructure.Storage;

/// <summary>
/// Turns trainers and photos into keys, and defers the rest to the object store.
/// </summary>
/// <remarks>
/// The key layout lives here and only here. It is <c>trainers/{trainerId}/{photoId}</c>: grouping
/// by trainer makes a prefix listing answer "what does this trainer have stored" without a
/// database, and naming the leaf after the photo rather than the trainer is what allows a
/// replacement to be written before the old one is deleted. A layout of
/// <c>trainers/{trainerId}</c> would force an overwrite in place and lose that property.
/// </remarks>
public sealed class TrainerPhotoStore(IObjectStore objectStore) : ITrainerPhotoStore
{
    /// <inheritdoc />
    public Task StoreAsync(
        TrainerId trainerId,
        TrainerPhoto photo,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(photo);

        return objectStore.PutAsync(KeyFor(trainerId, photo), content, photo.ContentType, cancellationToken);
    }

    /// <inheritdoc />
    public Task<StoredObject?> FetchAsync(
        TrainerId trainerId,
        TrainerPhoto photo,
        CancellationToken cancellationToken = default) =>
        objectStore.GetAsync(KeyFor(trainerId, photo), cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(
        TrainerId trainerId,
        TrainerPhoto photo,
        CancellationToken cancellationToken = default) =>
        objectStore.DeleteAsync(KeyFor(trainerId, photo), cancellationToken);

    private static ObjectKey KeyFor(TrainerId trainerId, TrainerPhoto photo)
    {
        ArgumentNullException.ThrowIfNull(trainerId);
        ArgumentNullException.ThrowIfNull(photo);

        return ObjectKey.Create(string.Create(
            CultureInfo.InvariantCulture,
            $"trainers/{trainerId.Value}/{photo.PhotoId}"));
    }
}
