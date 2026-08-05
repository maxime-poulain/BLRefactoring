using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace TrainingHub.Shared.Infrastructure.Storage;

/// <summary>
/// Proves the object store reachable with one signed, single-key list of the bucket.
/// </summary>
/// <remarks>
/// <c>ListObjectsV2</c> with <c>MaxKeys = 1</c> is chosen over a metadata stub on purpose: it is
/// a real read that proves the endpoint, the credentials and the bucket in one round-trip, where
/// a call like <c>GetBucketLocation</c> is exactly the kind of stub an S3-compatible server is
/// entitled to fake (ADR 0037). Exceptions travel to the caller untouched — the health check
/// translates a fault into a status, and this adapter has no opinion to add.
/// </remarks>
internal sealed class S3Reachability(IAmazonS3 client, IOptions<ObjectStorageOptions> options)
    : IObjectStoreReachability
{
    private readonly ObjectStorageOptions _options = options.Value;

    /// <inheritdoc />
    public Task ProbeAsync(CancellationToken cancellationToken = default) =>
        client.ListObjectsV2Async(
            new ListObjectsV2Request
            {
                BucketName = _options.BucketName,
                MaxKeys = 1
            },
            cancellationToken);
}
