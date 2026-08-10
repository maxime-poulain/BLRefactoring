using TrainingHub.DDD.Application.Services.TrainerServices;
using TrainingHub.Shared;
using TrainingHub.Shared.Application.Photos;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using Moq;

namespace TrainingHub.DDD.Application.Tests.Helpers;

/// <summary>
/// Trainer service test fixture.
/// </summary>
public sealed class TrainerServiceTestFixture
{
    /// <summary>
    /// Trainer repository.
    /// </summary>
    public Mock<ITrainerRepository> TrainerRepository { get; } = new();

    /// <summary>
    /// Current user service.
    /// </summary>
    public Mock<ICurrentUserService> CurrentUserService { get; } = new();

    /// <summary>
    /// Unit of work.
    /// </summary>
    public Mock<IUnitOfWork> UnitOfWork { get; } = new();

    /// <summary>
    /// Who the service will take the caller to be. Non-empty by default, because
    /// <c>TrainerId.Create</c> refuses <c>Guid.Empty</c> and a bare mock would throw before a test
    /// reached its assertion.
    /// </summary>
    public Guid CallerId { get; private set; } = Guid.NewGuid();

    /// <summary>
    /// Trainer service test fixture.
    /// </summary>
    public TrainerServiceTestFixture()
    {
        GivenCaller(CallerId);

        PhotoSanitiser
            .Setup(sanitiser => sanitiser.Sanitise(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<string>()))
            .Returns((ReadOnlyMemory<byte> content, string contentType) =>
                Result<SanitisedPhoto>.Success(new SanitisedPhoto(content, contentType)));
    }

    /// <summary>Makes the service resolve <paramref name="trainerId"/> as the calling trainer.</summary>
    /// <remarks>
    /// EditAsync no longer takes the trainer it edits — it serves <c>PUT /Trainer/me</c> and reads
    /// the caller itself — so a test states who is calling instead of passing an identifier.
    /// </remarks>
    public void GivenCaller(Guid trainerId)
    {
        CallerId = trainerId;
        CurrentUserService.SetupGet(service => service.TrainerId).Returns(trainerId);
    }

    /// <summary>
    /// Where the bytes of a photo would go.
    /// </summary>
    /// <remarks>
    /// A mock rather than a fake with storage behind it: the tests here are about the order the
    /// service does things in — bytes written before the row is committed, displaced bytes deleted
    /// only after — and that order is stated by verifying calls on this, not by looking in a
    /// bucket. The round trip is the integration suite's job.
    /// </remarks>
    public Mock<ITrainerPhotoStore> PhotoStore { get; } = new();

    /// <summary>
    /// What strips an upload before the aggregate ever sees it (ADR 0063).
    /// </summary>
    /// <remarks>
    /// A mock that hands the bytes straight back, for the same reason the store above is a mock:
    /// the tests here are about the order this service does things in, and the fixtures they upload
    /// are a valid signature followed by nothing in particular — a real decoder would refuse them,
    /// correctly, and every one of these tests would then be about the decoder. What the real one
    /// does to real pixels is <c>SkiaSharpPhotoSanitiserTests</c>.
    /// <para>
    /// It is still wired rather than bypassed, so a service that stopped calling it would fail the
    /// fact that says it must.
    /// </para>
    /// </remarks>
    public Mock<IPhotoSanitiser> PhotoSanitiser { get; } = new();

    /// <summary>
    /// The instant this fixture's clock is stopped at.
    /// </summary>
    public static readonly DateTime Now = new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// The clock the sanitisation stamp is read from, stopped so a fact can name the instant.
    /// </summary>
    /// <remarks>
    /// Declared here rather than pulled from a package, which is what the two infrastructure
    /// suites already do for the same need.
    /// </remarks>
    private sealed class StoppedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly TimeProvider _clock = new StoppedClock();

    /// <summary>
    /// Create sut.
    /// </summary>
    public TrainerApplicationService CreateSut() => new(
        TrainerRepository.Object,
        PhotoStore.Object,
        PhotoSanitiser.Object,
        CurrentUserService.Object,
        _clock,
        UnitOfWork.Object);
}
