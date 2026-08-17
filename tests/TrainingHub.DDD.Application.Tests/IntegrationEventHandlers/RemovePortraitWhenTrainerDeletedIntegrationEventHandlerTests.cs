using Moq;
using TrainingHub.Shared.Application.IntegrationEventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.IntegrationEventHandlers;

/// <summary>
/// Behavior covered for the portrait's collector: the bytes an erasure leaves behind (ADR 0085).
/// </summary>
public sealed class RemovePortraitWhenTrainerDeletedIntegrationEventHandlerTests
{
    private readonly Mock<ITrainerPhotoStore> _photoStore = new();

    /// <summary>
    /// Handle, deletes the bytes the fact names.
    /// </summary>
    [Fact]
    public async Task Handle_DeletesTheBytesTheFactNames()
    {
        // Arrange
        var trainerId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        var fact = new TrainerDeletedIntegrationEvent(trainerId, photoId);

        var sut = new RemovePortraitWhenTrainerDeletedIntegrationEventHandler(_photoStore.Object);

        // Act
        await sut.HandleAsync(fact, CancellationToken.None);

        // Assert
        _photoStore.Verify(store => store.DeleteAsync(
                It.Is<TrainerId>(id => id.Value == trainerId),
                It.Is<PhotoId>(id => id.Value == photoId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Handle, a trainer who had no portrait, deletes nothing.
    /// </summary>
    [Fact]
    public async Task Handle_ATrainerWhoHadNoPortrait_DeletesNothing()
    {
        // Arrange
        var fact = new TrainerDeletedIntegrationEvent(Guid.NewGuid(), PhotoId: null);

        var sut = new RemovePortraitWhenTrainerDeletedIntegrationEventHandler(_photoStore.Object);

        // Act
        await sut.HandleAsync(fact, CancellationToken.None);

        // Assert
        _photoStore.Verify(store => store.DeleteAsync(
                It.IsAny<TrainerId>(), It.IsAny<PhotoId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
