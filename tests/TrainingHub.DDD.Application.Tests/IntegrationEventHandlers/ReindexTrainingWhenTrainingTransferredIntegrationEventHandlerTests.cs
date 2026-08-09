using TrainingHub.Shared.Application.Search;
using TrainingHub.Shared.Application.IntegrationEventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using Moq;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.IntegrationEventHandlers;

/// <summary>
/// Behaviour covered for <c>ReindexTrainingWhenTrainingTransferredIntegrationEventHandler</c>.
/// </summary>
public sealed class ReindexTrainingWhenTrainingTransferredIntegrationEventHandlerTests
{
    private readonly Mock<ITrainingSearchIndexer> _searchIndexer = new();

    /// <summary>
    /// Handle, reindexes the training under its new owner.
    /// </summary>
    [Fact]
    public async Task Handle_ReindexesTheTraining_UnderItsNewOwner()
    {
        var trainingId = Guid.NewGuid();
        var formerOwner = Guid.NewGuid();
        var newOwner = Guid.NewGuid();

        var sut = new ReindexTrainingWhenTrainingTransferredIntegrationEventHandler(_searchIndexer.Object);
        await sut.HandleAsync(
            new TrainingTransferredIntegrationEvent(trainingId, formerOwner, newOwner),
            CancellationToken.None);

        // The upsert converges on the training id, so indexing under the new owner IS the move.
        _searchIndexer.Verify(
            s => s.IndexAsync(trainingId, newOwner, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
