namespace TrainingHub.Shared.Storage;

/// <summary>
/// Holds bytes under a key. It knows nothing else.
/// </summary>
/// <remarks>
/// <para>
/// Three operations, and deliberately not a fourth. There is no <c>Replace</c>, because replacing
/// is not one step: it is writing a <em>new</em> key, committing the row that names it, and only
/// then deleting the old one. An overwrite-in-place method would invite the opposite ordering,
/// which is the single ordering that can leave a committed row pointing at bytes that are gone.
/// An architecture rule holds this interface to exactly these three members for that reason.
/// </para>
/// <para>
/// Nothing here mentions a bucket, an endpoint or a URL, so nothing above the infrastructure
/// depends on which store is behind it. The adapter in this solution speaks S3, which is what
/// SeaweedFS serves locally and what every hosted object store on the shortlist also serves — so
/// changing provider is configuration, not code. See ADR 0021.
/// </para>
/// </remarks>
public interface IObjectStore
{
    /// <summary>
    /// Writes bytes under a key, replacing whatever was there.
    /// </summary>
    /// <param name="key">The name to store them under.</param>
    /// <param name="content">The bytes.</param>
    /// <param name="contentType">The media type to store alongside them.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the bytes are stored.</returns>
    Task PutAsync(
        ObjectKey key,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the bytes stored under a key.
    /// </summary>
    /// <param name="key">The name to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The object, or <see langword="null"/> when the key holds nothing.</returns>
    /// <remarks>
    /// A missing key is an answer, not an exception: a photo can legitimately have been deleted
    /// between a page being rendered and its image being fetched.
    /// </remarks>
    Task<StoredObject?> GetAsync(ObjectKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes whatever is stored under a key.
    /// </summary>
    /// <param name="key">The name to delete.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>A task that completes when the key holds nothing.</returns>
    /// <remarks>
    /// Deleting a key that holds nothing succeeds. Callers delete displaced photos after the fact
    /// and may retry; a second attempt is not an error.
    /// </remarks>
    Task DeleteAsync(ObjectKey key, CancellationToken cancellationToken = default);
}
