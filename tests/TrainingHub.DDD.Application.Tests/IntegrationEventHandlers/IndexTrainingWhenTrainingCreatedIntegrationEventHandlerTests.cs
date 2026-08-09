using TrainingHub.Shared.Application.Search;
using TrainingHub.Shared.Application.IntegrationEventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using Moq;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.IntegrationEventHandlers;

/// <summary>
/// Behaviour covered for <c>IndexTrainingWhenTrainingCreatedIntegrationEventHandler</c>.
/// </summary>
public sealed class IndexTrainingWhenTrainingCreatedIntegrationEventHandlerTests
{
    private readonly Mock<ITrainingSearchIndexer> _searchIndexer = new();

    /// <summary>
    /// Handle, indexes the training under both identifiers.
    /// </summary>
    [Fact]
    public async Task Handle_IndexesTheTraining_UnderBothIdentifiers()
    {
        var trainingId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();

        var sut = new IndexTrainingWhenTrainingCreatedIntegrationEventHandler(_searchIndexer.Object);
        await sut.HandleAsync(new TrainingCreatedIntegrationEvent(trainingId, trainerId), CancellationToken.None);

        _searchIndexer.Verify(
            s => s.IndexAsync(trainingId, trainerId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
