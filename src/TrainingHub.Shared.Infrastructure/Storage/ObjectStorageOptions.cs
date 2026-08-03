namespace TrainingHub.Shared.Infrastructure.Storage;

/// <summary>
/// Where the object store is and how to authenticate against it.
/// </summary>
/// <remarks>
/// Everything that ties this solution to one storage provider is in this class and nowhere else.
/// Pointing at a hosted store instead of the local container means changing
/// <see cref="ServiceUrl"/> and the credentials — no code, which is the claim ADR 0021 makes and
/// the reason the adapter speaks S3 rather than any provider's own dialect.
/// </remarks>
public sealed class ObjectStorageOptions
{
    /// <summary>
    /// The configuration section these options are bound from.
    /// </summary>
    public const string SectionName = "ObjectStorage";

    /// <summary>
    /// The endpoint of the store.
    /// </summary>
    public string ServiceUrl { get; set; } = null!;

    /// <summary>
    /// The bucket photos are written to.
    /// </summary>
    public string BucketName { get; set; } = null!;

    /// <summary>
    /// The access key.
    /// </summary>
    public string AccessKey { get; set; } = null!;

    /// <summary>
    /// The secret key.
    /// </summary>
    public string SecretKey { get; set; } = null!;

    /// <summary>
    /// Whether the bucket is created at start-up when it does not exist.
    /// </summary>
    /// <remarks>
    /// True for the local container, which starts empty every time; a hosted bucket is provisioned
    /// once by whoever owns the account, and an application that creates its own would need
    /// permissions it has no other use for.
    /// </remarks>
    public bool CreateBucketOnStartup { get; set; }
}
