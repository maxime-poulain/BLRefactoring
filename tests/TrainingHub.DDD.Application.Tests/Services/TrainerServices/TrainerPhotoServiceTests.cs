using AwesomeAssertions;
using TrainingHub.DDD.Application.Tests.Helpers;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using TrainingHub.Shared.Storage;
using Moq;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.Services.TrainerServices;

/// <summary>
/// Behaviour covered for the photo use cases of <c>TrainerApplicationService</c>.
/// </summary>
/// <remarks>
/// The rules about what an image may be are proven on the value object, where they live. What is
/// proven here is the thing only the service decides: the order it touches the object store and
/// the database in. That order is the reason a committed profile can never name bytes that are
/// not there, and nothing else in the codebase states it.
/// </remarks>
public sealed class TrainerPhotoServiceTests
{
    private readonly TrainerServiceTestFixture _fixture = new();

    /// <summary>
    /// Set photo async, valid image, stores the bytes before committing the row that names them.
    /// </summary>
    [Fact]
    public async Task SetPhotoAsync_ValidImage_StoresTheBytesBeforeCommittingTheRow()
    {
        // Arrange
        var trainer = GivenTrainerWithoutPhoto();
        var order = new List<string>();

        _fixture.PhotoStore
            .Setup(store => store.StoreAsync(
                It.IsAny<TrainerId>(),
                It.IsAny<TrainerPhoto>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("store"))
            .Returns(Task.CompletedTask);

        _fixture.UnitOfWork
            .Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("commit"))
            .ReturnsAsync(1);

        var sut = _fixture.CreateSut();

        // Act
        var result = await sut.SetPhotoAsync(Png(), TrainerPhoto.PngContentType);

        // Assert
        result.ShouldBeSuccess();
        order.Should().Equal("store", "commit");
        trainer.Photo.Should().NotBeNull();
    }

    /// <summary>
    /// Set photo async, replacing a photo, deletes the old bytes only after the commit.
    /// </summary>
    /// <remarks>
    /// The failure this rules out: deleting first, then failing to commit, and leaving the profile
    /// pointing at an object that no longer exists. Deleting last means the worst case is an
    /// orphaned object nobody references, which is rubbish rather than corruption.
    /// </remarks>
    [Fact]
    public async Task SetPhotoAsync_ReplacingAPhoto_DeletesTheOldBytesAfterTheCommit()
    {
        // Arrange
        var trainer = GivenTrainerWithoutPhoto();
        var previous = SomePhoto();
        trainer.AttachPhoto(previous);

        var order = new List<string>();

        _fixture.PhotoStore
            .Setup(store => store.StoreAsync(
                It.IsAny<TrainerId>(),
                It.IsAny<TrainerPhoto>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("store"))
            .Returns(Task.CompletedTask);

        _fixture.UnitOfWork
            .Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("commit"))
            .ReturnsAsync(1);

        _fixture.PhotoStore
            .Setup(store => store.DeleteAsync(
                It.IsAny<TrainerId>(),
                It.IsAny<TrainerPhoto>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("delete"))
            .Returns(Task.CompletedTask);

        var sut = _fixture.CreateSut();

        // Act
        await sut.SetPhotoAsync(Png(), TrainerPhoto.PngContentType);

        // Assert
        order.Should().Equal("store", "commit", "delete");
    }

    /// <summary>
    /// Set photo async, replacing a photo, deletes the photo it displaced and not the new one.
    /// </summary>
    [Fact]
    public async Task SetPhotoAsync_ReplacingAPhoto_DeletesTheOneItDisplaced()
    {
        // Arrange
        var trainer = GivenTrainerWithoutPhoto();
        var previous = SomePhoto();
        trainer.AttachPhoto(previous);

        var sut = _fixture.CreateSut();

        // Act
        await sut.SetPhotoAsync(Png(), TrainerPhoto.PngContentType);

        // Assert
        _fixture.PhotoStore.Verify(
            store => store.DeleteAsync(trainer.Id, previous, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Set photo async, first photo, deletes nothing.
    /// </summary>
    [Fact]
    public async Task SetPhotoAsync_FirstPhoto_DeletesNothing()
    {
        // Arrange
        GivenTrainerWithoutPhoto();
        var sut = _fixture.CreateSut();

        // Act
        await sut.SetPhotoAsync(Png(), TrainerPhoto.PngContentType);

        // Assert
        _fixture.PhotoStore.Verify(
            store => store.DeleteAsync(
                It.IsAny<TrainerId>(), It.IsAny<TrainerPhoto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Set photo async, refused image, never reaches the store.
    /// </summary>
    /// <remarks>
    /// Validation comes first for a reason worth a test: bytes written for an upload that is then
    /// rejected would be rubbish nobody ever asked for, produced by a request that failed.
    /// </remarks>
    [Fact]
    public async Task SetPhotoAsync_RefusedImage_NeverReachesTheStore()
    {
        // Arrange
        GivenTrainerWithoutPhoto();
        var sut = _fixture.CreateSut();

        // Act
        var result = await sut.SetPhotoAsync("GIF89a not an image we take"u8.ToArray(), TrainerPhoto.PngContentType);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.PhotoFormatNotSupported);
        _fixture.PhotoStore.Verify(
            store => store.StoreAsync(
                It.IsAny<TrainerId>(),
                It.IsAny<TrainerPhoto>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Set photo async, unknown trainer, returns not found.
    /// </summary>
    [Fact]
    public async Task SetPhotoAsync_UnknownTrainer_ReturnsNotFound()
    {
        // Arrange
        _fixture.TrainerRepository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trainer?)null);

        var sut = _fixture.CreateSut();

        // Act
        var result = await sut.SetPhotoAsync(Png(), TrainerPhoto.PngContentType);

        // Assert
        result.ShouldContainError(ErrorCodes.NotFound);
    }

    /// <summary>
    /// Remove photo async, photo published, commits before deleting the bytes.
    /// </summary>
    [Fact]
    public async Task RemovePhotoAsync_PhotoPublished_CommitsBeforeDeletingTheBytes()
    {
        // Arrange
        var trainer = GivenTrainerWithoutPhoto();
        var photo = SomePhoto();
        trainer.AttachPhoto(photo);

        var order = new List<string>();

        _fixture.UnitOfWork
            .Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("commit"))
            .ReturnsAsync(1);

        _fixture.PhotoStore
            .Setup(store => store.DeleteAsync(
                It.IsAny<TrainerId>(), It.IsAny<TrainerPhoto>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("delete"))
            .Returns(Task.CompletedTask);

        var sut = _fixture.CreateSut();

        // Act
        var result = await sut.RemovePhotoAsync();

        // Assert
        result.ShouldBeSuccess();
        order.Should().Equal("commit", "delete");
        trainer.Photo.Should().BeNull();
    }

    /// <summary>
    /// Remove photo async, no photo, returns not found and touches nothing.
    /// </summary>
    [Fact]
    public async Task RemovePhotoAsync_NoPhoto_ReturnsNotFound()
    {
        // Arrange
        GivenTrainerWithoutPhoto();
        var sut = _fixture.CreateSut();

        // Act
        var result = await sut.RemovePhotoAsync();

        // Assert
        result.ShouldContainError(ErrorCodes.NotFound);
        _fixture.UnitOfWork.Verify(
            unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Get photo async, photo published, returns the bytes the store holds.
    /// </summary>
    [Fact]
    public async Task GetPhotoAsync_PhotoPublished_ReturnsTheBytesTheStoreHolds()
    {
        // Arrange
        var trainer = GivenTrainerWithoutPhoto();
        var photo = SomePhoto();
        trainer.AttachPhoto(photo);

        _fixture.PhotoStore
            .Setup(store => store.FetchAsync(trainer.Id, photo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredObject(Png(), TrainerPhoto.PngContentType));

        var sut = _fixture.CreateSut();

        // Act
        var result = await sut.GetPhotoAsync(trainer.Id.Value);

        // Assert
        result.Should().NotBeNull();
        result!.PhotoId.Should().Be(photo.PhotoId);
        result.ContentType.Should().Be(TrainerPhoto.PngContentType);
    }

    /// <summary>
    /// Get photo async, no photo, returns nothing without asking the store.
    /// </summary>
    [Fact]
    public async Task GetPhotoAsync_NoPhoto_ReturnsNothingWithoutAskingTheStore()
    {
        // Arrange
        var trainer = GivenTrainerWithoutPhoto();
        var sut = _fixture.CreateSut();

        // Act
        var result = await sut.GetPhotoAsync(trainer.Id.Value);

        // Assert
        result.Should().BeNull();
        _fixture.PhotoStore.Verify(
            store => store.FetchAsync(
                It.IsAny<TrainerId>(), It.IsAny<TrainerPhoto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Get photo async, row naming bytes the store lost, answers no photo rather than throwing.
    /// </summary>
    [Fact]
    public async Task GetPhotoAsync_BytesMissingFromTheStore_AnswersNoPhoto()
    {
        // Arrange
        var trainer = GivenTrainerWithoutPhoto();
        trainer.AttachPhoto(SomePhoto());

        _fixture.PhotoStore
            .Setup(store => store.FetchAsync(
                It.IsAny<TrainerId>(), It.IsAny<TrainerPhoto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StoredObject?)null);

        var sut = _fixture.CreateSut();

        // Act
        var result = await sut.GetPhotoAsync(trainer.Id.Value);

        // Assert
        result.Should().BeNull();
    }

    private Trainer GivenTrainerWithoutPhoto()
    {
        var trainer = new TrainerBuilder().WithId(_fixture.CallerId).Build();

        _fixture.TrainerRepository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<TrainerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainer);

        return trainer;
    }

    private static TrainerPhoto SomePhoto() =>
        TrainerPhoto.Create(Png(), TrainerPhoto.PngContentType).ShouldBeSuccess();

    private static byte[] Png()
    {
        var content = new byte[64];
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(content);

        return content;
    }
}
