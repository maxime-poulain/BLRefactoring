using AwesomeAssertions;
using TrainingHub.DDDWithCqrs.Application.Features.Trainers.RemovePhoto;
using TrainingHub.DDDWithCqrs.Application.Features.Trainers.SetPhoto;
using TrainingHub.Shared;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Application.Photos;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Common.Results;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Moq;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Tests.Handlers;

/// <summary>
/// Behaviour covered for the photo command handlers.
/// </summary>
/// <remarks>
/// The same ordering the layered stack is held to, held to here as well. Both hosts publish the
/// same operations, so both have to make the same promise about what a crash can leave behind —
/// and a promise proven on one stack only is a promise on one stack only.
/// </remarks>
public sealed class TrainerPhotoCommandHandlerTests
{
    private readonly Mock<ITrainerRepository> _trainerRepository = new();
    private readonly Mock<ITrainerPhotoStore> _photoStore = new();

    /// <summary>
    /// What strips an upload before the aggregate sees it (ADR 0063).
    /// </summary>
    /// <remarks>
    /// A mock handing the bytes back unchanged, for the same reason the store beside it is one:
    /// these facts are about the order this handler does things in, and the fixture it uploads is a
    /// signature followed by nothing — a real decoder would refuse it and every fact here would
    /// become a fact about the decoder. <c>SkiaSharpPhotoSanitiserTests</c> is where real pixels go.
    /// </remarks>
    private readonly Mock<IPhotoSanitiser> _photoSanitiser = new();

    /// <summary>The instant the stamp is read from.</summary>
    private static readonly DateTime SanitisedAt = new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);

    private sealed class StoppedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => SanitisedAt;
    }

    private readonly TimeProvider _clock = new StoppedClock();
    private readonly Mock<ICurrentUserService> _currentUserService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Guid _callerId = Guid.NewGuid();
    private readonly List<string> _order = [];

    /// <summary>
    /// Trainer photo command handler tests.
    /// </summary>
    public TrainerPhotoCommandHandlerTests()
    {
        _currentUserService.SetupGet(service => service.TrainerId).Returns(_callerId);

        _photoSanitiser
            .Setup(sanitiser => sanitiser.Sanitise(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<string>()))
            .Returns((ReadOnlyMemory<byte> content, string contentType) =>
                Result<SanitisedPhoto>.Success(new SanitisedPhoto(content, contentType)));

        _photoStore
            .Setup(store => store.StoreAsync(
                It.IsAny<TrainerId>(),
                It.IsAny<TrainerPhoto>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("store"))
            .Returns(Task.CompletedTask);

        _unitOfWork
            .Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("commit"))
            .ReturnsAsync(1);

        _photoStore
            .Setup(store => store.DeleteAsync(
                It.IsAny<TrainerId>(), It.IsAny<TrainerPhoto>(), It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("delete"))
            .Returns(Task.CompletedTask);
    }

    /// <summary>
    /// Handle, first photo, stores the bytes then commits.
    /// </summary>
    [Fact]
    public async Task Handle_FirstPhoto_StoresTheBytesThenCommits()
    {
        // Arrange
        var trainer = GivenTrainerWithoutPhoto();

        // Act
        var result = await SetSut().Handle(SetCommand(), CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        _order.Should().Equal("store", "commit");
        trainer.Photo.Should().NotBeNull();
    }

    /// <summary>
    /// Handle, replacing a photo, deletes the displaced bytes only after the commit.
    /// </summary>
    [Fact]
    public async Task Handle_ReplacingAPhoto_DeletesTheDisplacedBytesAfterTheCommit()
    {
        // Arrange
        var trainer = GivenTrainerWithoutPhoto();
        var previous = SomePhoto();
        trainer.AttachPhoto(previous);

        // Act
        await SetSut().Handle(SetCommand(), CancellationToken.None);

        // Assert
        _order.Should().Equal("store", "commit", "delete");
        _photoStore.Verify(
            store => store.DeleteAsync(trainer.Id, previous, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Handle, image the domain refuses, never reaches the store.
    /// </summary>
    [Fact]
    public async Task Handle_RefusedImage_NeverReachesTheStore()
    {
        // Arrange
        GivenTrainerWithoutPhoto();

        // Act
        var result = await SetSut().Handle(
            SetCommand(content: "GIF89a not an image we take"u8.ToArray()), CancellationToken.None);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.PhotoFormatNotSupported);
        _order.Should().BeEmpty();
    }

    /// <summary>
    /// Handle, declared type disagreeing with the bytes, is refused with the mismatch code.
    /// </summary>
    /// <remarks>
    /// Not the pipeline's validation code, deliberately: that one means the command was rejected
    /// before the domain ever saw it, and this rejection came from reading the bytes.
    /// </remarks>
    [Fact]
    public async Task Handle_DeclaredTypeDisagreeingWithTheBytes_IsRefusedWithTheMismatchCode()
    {
        // Arrange
        GivenTrainerWithoutPhoto();

        // Act
        var result = await SetSut().Handle(
            SetCommand(content: Jpeg()), CancellationToken.None);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.PhotoContentMismatch);
    }

    /// <summary>
    /// Handle, a losing race on commit, reports a conflict and leaves rubbish rather than a broken row.
    /// </summary>
    [Fact]
    public async Task Handle_LosingRaceOnCommit_ReportsAConflict()
    {
        // Arrange
        GivenTrainerWithoutPhoto();

        _unitOfWork
            .Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("beaten to it", new InvalidOperationException()));

        // Act
        var result = await SetSut().Handle(SetCommand(), CancellationToken.None);

        // Assert
        result.ShouldContainError(ErrorCodes.ConcurrencyConflict);
        _order.Should().Equal("store");
    }

    /// <summary>
    /// Handle, unknown trainer, returns not found.
    /// </summary>
    [Fact]
    public async Task Handle_UnknownTrainer_ReturnsNotFound()
    {
        // Arrange
        _trainerRepository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trainer?)null);

        // Act
        var result = await SetSut().Handle(SetCommand(), CancellationToken.None);

        // Assert
        result.ShouldContainError(ErrorCodes.NotFound);
    }

    /// <summary>
    /// Handle remove, photo published, commits then deletes.
    /// </summary>
    [Fact]
    public async Task HandleRemove_PhotoPublished_CommitsThenDeletes()
    {
        // Arrange
        var trainer = GivenTrainerWithoutPhoto();
        trainer.AttachPhoto(SomePhoto());

        // Act
        var result = await RemoveSut().Handle(new RemoveTrainerPhotoCommand(), CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        _order.Should().Equal("commit", "delete");
        trainer.Photo.Should().BeNull();
    }

    /// <summary>
    /// Handle remove, no photo, returns not found and commits nothing.
    /// </summary>
    [Fact]
    public async Task HandleRemove_NoPhoto_ReturnsNotFound()
    {
        // Arrange
        GivenTrainerWithoutPhoto();

        // Act
        var result = await RemoveSut().Handle(new RemoveTrainerPhotoCommand(), CancellationToken.None);

        // Assert
        result.ShouldContainError(ErrorCodes.NotFound);
        _order.Should().BeEmpty();
    }

    private SetTrainerPhotoCommandHandler SetSut() => new(
        _trainerRepository.Object,
        _photoStore.Object,
        _photoSanitiser.Object,
        _currentUserService.Object,
        _clock,
        _unitOfWork.Object);

    private RemoveTrainerPhotoCommandHandler RemoveSut() => new(
        _trainerRepository.Object,
        _photoStore.Object,
        _currentUserService.Object,
        _unitOfWork.Object);

    private static SetTrainerPhotoCommand SetCommand(byte[]? content = null) => new()
    {
        Content = content ?? Png(),
        ContentType = TrainerPhoto.PngContentType
    };

    private Trainer GivenTrainerWithoutPhoto()
    {
        var trainer = new TrainerBuilder().WithId(_callerId).Build();

        _trainerRepository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);

        return trainer;
    }

    private static TrainerPhoto SomePhoto() =>
        TrainerPhoto.Create(Png(), TrainerPhoto.PngContentType, DateTime.UtcNow).ShouldBeSuccess();

    private static byte[] Png() => Padded([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

    private static byte[] Jpeg() => Padded([0xFF, 0xD8, 0xFF, 0xE0]);

    private static byte[] Padded(ReadOnlySpan<byte> signature)
    {
        var content = new byte[64];
        signature.CopyTo(content);

        return content;
    }
}
