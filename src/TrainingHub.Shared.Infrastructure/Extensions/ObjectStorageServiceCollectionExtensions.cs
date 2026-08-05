using Amazon.Runtime;
using Amazon.S3;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Infrastructure.Storage;
using TrainingHub.Shared.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TrainingHub.Shared.Infrastructure.Extensions;

/// <summary>
/// Registers the object store and the photo store that sits on it.
/// </summary>
public static class ObjectStorageServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="ObjectStorageOptions"/> and wires the S3 adapter behind
    /// <see cref="IObjectStore"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration carrying the <c>ObjectStorage</c> section.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// The client is built by hand rather than through <c>AWSSDK.Extensions.NETCore.Setup</c>: that
    /// package exists to discover ambient AWS credentials and regions, and there are none here to
    /// discover. Everything this needs is in one configuration section, and saying so takes fewer
    /// lines than explaining why the discovery found nothing.
    /// </remarks>
    public static IServiceCollection AddObjectStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<ObjectStorageOptions>()
            .Bind(configuration.GetSection(ObjectStorageOptions.SectionName))
            // Fail at start-up rather than on the first upload. A store this host cannot reach is
            // a deployment mistake, and the moment to hear about it is before traffic arrives.
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ServiceUrl),
                $"Missing configuration value '{ObjectStorageOptions.SectionName}:ServiceUrl'.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.BucketName),
                $"Missing configuration value '{ObjectStorageOptions.SectionName}:BucketName'.")
            .ValidateOnStart();

        return services
            .AddSingleton<IAmazonS3>(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<ObjectStorageOptions>>().Value;

                return new AmazonS3Client(
                    new BasicAWSCredentials(options.AccessKey, options.SecretKey),
                    new AmazonS3Config
                    {
                        ServiceURL = options.ServiceUrl,
                        // A self-hosted endpoint has no per-bucket DNS, so the bucket has to travel
                        // in the path. Without this the client would resolve
                        // `bucket.localhost:8333` and fail to find anything at all.
                        ForcePathStyle = true,
                        // Nothing here is on AWS, so there is no region to speak of — but the SDK
                        // signs every request and a signature needs one. This is the value the
                        // S3-compatible servers on the shortlist all accept.
                        AuthenticationRegion = "us-east-1",
                        // Version 4 of the SDK computes a CRC32 for every upload by default and
                        // sends it with `Content-Encoding: aws-chunked`, framing the body in
                        // length-prefixed chunks. Amazon's own S3 understands that framing; a
                        // compatible implementation is entitled not to, and SeaweedFS stores the
                        // frame markers as though they were part of the image. Every upload came
                        // back 500 until these two lines existed, and nothing about the message
                        // pointed at a checksum.
                        //
                        // WHEN_REQUIRED keeps the checksums the protocol genuinely demands and
                        // drops the ones added on speculation. It is also what makes this adapter
                        // portable in practice rather than in principle: the default is the single
                        // most common reason SDK v4 fails against a non-AWS endpoint.
                        RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                        ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
                    });
            })
            .AddSingleton<IObjectStore, S3ObjectStore>()
            .AddSingleton<IObjectStoreReachability, S3Reachability>()
            .AddSingleton<ITrainerPhotoStore, TrainerPhotoStore>()
            .AddHostedService<ObjectStorageBootstrapper>();
    }
}
