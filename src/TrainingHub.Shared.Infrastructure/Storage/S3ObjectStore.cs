using Amazon.S3;
using Amazon.S3.Model;
using TrainingHub.Shared.Storage;
using Microsoft.Extensions.Options;

namespace TrainingHub.Shared.Infrastructure.Storage;

/// <summary>
/// An <see cref="IObjectStore"/> over anything that speaks S3.
/// </summary>
/// <remarks>
/// This is the only type in the solution that knows the storage protocol, and an architecture rule
/// keeps it that way. It talks to a SeaweedFS container in development and would talk to a hosted
/// bucket in production without a line changing — path-style addressing is what makes the two
/// interchangeable, since a self-hosted endpoint has no per-bucket DNS to put a bucket name in
/// front of.
/// </remarks>
public sealed class S3ObjectStore(IAmazonS3 client, IOptions<ObjectStorageOptions> options) : IObjectStore
{
    private readonly ObjectStorageOptions _options = options.Value;

    /// <inheritdoc />
    public async Task PutAsync(
        ObjectKey key,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        using var stream = new MemoryStream(content.ToArray(), writable: false);

        await client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = key.Value,
                InputStream = stream,
                ContentType = contentType
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<StoredObject?> GetAsync(ObjectKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        try
        {
            using var response = await client.GetObjectAsync(
                _options.BucketName, key.Value, cancellationToken);

            using var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, cancellationToken);

            return new StoredObject(buffer.ToArray(), response.Headers.ContentType);
        }
        catch (AmazonS3Exception exception) when (IsMissing(exception))
        {
            // Absence is an answer here, not a fault: a photo can be deleted between a page being
            // rendered and the browser asking for its image. The interface says so by returning
            // null, so the exception stops at the only layer that knows S3 signals it as one.
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(ObjectKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        try
        {
            await client.DeleteObjectAsync(_options.BucketName, key.Value, cancellationToken);
        }
        catch (AmazonS3Exception exception) when (IsMissing(exception))
        {
            // Deleting what is already gone is the outcome the caller asked for. Displaced photos
            // are cleaned up after the fact and the cleanup may be retried; a second attempt is
            // not a failure worth propagating.
        }
    }

    private static bool IsMissing(AmazonS3Exception exception) =>
        exception.StatusCode is System.Net.HttpStatusCode.NotFound;
}
