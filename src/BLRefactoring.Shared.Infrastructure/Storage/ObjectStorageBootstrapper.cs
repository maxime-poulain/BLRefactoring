using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BLRefactoring.Shared.Infrastructure.Storage;

/// <summary>
/// Creates the bucket at start-up when it is not there.
/// </summary>
/// <remarks>
/// <para>
/// The counterpart of migrating the database on start-up in Development: the local container comes
/// up empty every time, and a first upload failing because a bucket is missing is a worse way to
/// learn that than a line in the log. Off by default — a hosted bucket is provisioned once by
/// whoever owns the account, and an application that creates its own needs a permission it has no
/// other use for.
/// </para>
/// <para>
/// It stands down while the OpenAPI document is being produced, for the same reason the migrations
/// do. That tooling loads the application and runs it, hosted services included — which was worth
/// finding out the hard way: a bucket bootstrapper with no store to reach failed the build of the
/// generated HTTP client, several layers away from anything a reader would connect to storage.
/// </para>
/// </remarks>
public sealed class ObjectStorageBootstrapper(
    IAmazonS3 client,
    IOptions<ObjectStorageOptions> options,
    ILogger<ObjectStorageBootstrapper> logger) : IHostedService
{
    private readonly ObjectStorageOptions _options = options.Value;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.CreateBucketOnStartup || OpenApiDocumentGeneration.IsInProgress())
        {
            return;
        }

        try
        {
            await client.PutBucketAsync(
                new PutBucketRequest { BucketName = _options.BucketName }, cancellationToken);

            logger.LogInformation("Object storage: bucket {Bucket} is ready.", _options.BucketName);
        }
        catch (AmazonS3Exception exception) when (AlreadyThere(exception))
        {
            logger.LogInformation("Object storage: bucket {Bucket} already exists.", _options.BucketName);
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static bool AlreadyThere(AmazonS3Exception exception) =>
        string.Equals(exception.ErrorCode, "BucketAlreadyOwnedByYou", StringComparison.Ordinal)
        || string.Equals(exception.ErrorCode, "BucketAlreadyExists", StringComparison.Ordinal);
}
