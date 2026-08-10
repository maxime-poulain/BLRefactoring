using TrainingHub.Shared.Application.Search;
using TrainingHub.Shared.Application.IntegrationEventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using Moq;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.IntegrationEventHandlers;

/// <summary>
/// Behavior covered for <c>ReindexTrainingWhenTrainingEditedIntegrationEventHandler</c>.
/// </summary>
public sealed class ReindexTrainingWhenTrainingEditedIntegrationEventHandlerTests
{
    private readonly Mock<ITrainingSearchIndexer> _searchIndexer = new();

    /// <summary>
    /// Handle, reindexes the training under both identifiers.
    /// </summary>
    [Fact]
    public async Task Handle_ReindexesTheTraining_UnderBothIdentifiers()
    {
        var trainingId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();

        var sut = new ReindexTrainingWhenTrainingEditedIntegrationEventHandler(_searchIndexer.Object);
        await sut.HandleAsync(new TrainingEditedIntegrationEvent(trainingId, trainerId), CancellationToken.None);

        _searchIndexer.Verify(
            s => s.IndexAsync(trainingId, trainerId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
