using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using TrainingHub.Shared.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Storage;

/// <summary>
/// Behaviour covered for <c>ObjectStorageBootstrapper</c>.
/// </summary>
/// <remarks>
/// What is worth pinning here is when this does <em>nothing</em>. Creating a bucket that is missing
/// is the easy half; the decisions are that it stays out of the way where a bucket is provisioned
/// by somebody else, and that an existing bucket is not an error. The third case — standing down
/// while the OpenAPI document is being produced — cannot be staged from a test, because the signal
/// is partly the identity of the process the test itself runs in.
/// </remarks>
public sealed class ObjectStorageBootstrapperTests
{
    private readonly Mock<IAmazonS3> _client = new();

    /// <summary>
    /// Start async, creation turned on, creates the bucket.
    /// </summary>
    [Fact]
    public async Task StartAsync_CreationTurnedOn_CreatesTheBucket()
    {
        // Act
        await CreateSut(createBucketOnStartup: true).StartAsync(CancellationToken.None);

        // Assert
        _client.Verify(
            client => client.PutBucketAsync(
                It.Is<PutBucketRequest>(request => request.BucketName == "photos"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Start async, creation turned off, touches nothing.
    /// </summary>
    /// <remarks>
    /// The default, and the one that matters in a deployment: an application that creates its own
    /// bucket needs a permission it has no other use for.
    /// </remarks>
    [Fact]
    public async Task StartAsync_CreationTurnedOff_TouchesNothing()
    {
        // Act
        await CreateSut(createBucketOnStartup: false).StartAsync(CancellationToken.None);

        // Assert
        _client.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Start async, bucket already there, is not an error.
    /// </summary>
    /// <remarks>
    /// The local container is recreated far more often than its volume is emptied, so this is the
    /// ordinary case rather than the exceptional one.
    /// </remarks>
    [Fact]
    public async Task StartAsync_BucketAlreadyThere_IsNotAnError()
    {
        // Arrange
        _client
            .Setup(client => client.PutBucketAsync(
                It.IsAny<PutBucketRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("already yours") { ErrorCode = "BucketAlreadyOwnedByYou" });

        // Act
        var act = () => CreateSut(createBucketOnStartup: true).StartAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// Start async, refused for any other reason, fails the host.
    /// </summary>
    /// <remarks>
    /// A store this host cannot write to is a deployment mistake, and start-up is the moment to
    /// hear about it — not the first upload, from a user.
    /// </remarks>
    [Fact]
    public async Task StartAsync_RefusedForAnyOtherReason_FailsTheHost()
    {
        // Arrange
        _client
            .Setup(client => client.PutBucketAsync(
                It.IsAny<PutBucketRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("no") { ErrorCode = "AccessDenied" });

        // Act
        var act = () => CreateSut(createBucketOnStartup: true).StartAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AmazonS3Exception>();
    }

    /// <summary>
    /// Stop async, does nothing.
    /// </summary>
    [Fact]
    public async Task StopAsync_DoesNothing()
    {
        // Act
        await CreateSut(createBucketOnStartup: true).StopAsync(CancellationToken.None);

        // Assert
        _client.VerifyNoOtherCalls();
    }

    private ObjectStorageBootstrapper CreateSut(bool createBucketOnStartup) => new(
        _client.Object,
        Options.Create(new ObjectStorageOptions
        {
            ServiceUrl = "http://localhost:8333",
            BucketName = "photos",
            AccessKey = "irrelevant",
            SecretKey = "irrelevant",
            CreateBucketOnStartup = createBucketOnStartup
        }),
        NullLogger<ObjectStorageBootstrapper>.Instance);
}
