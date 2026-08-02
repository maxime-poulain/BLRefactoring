using BLRefactoring.Shared;
using BLRefactoring.Shared.Application.EventHandlers;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;
using Moq;
using Xunit;

namespace BLRefactoring.DDD.Application.Tests.EventHandlers;

/// <summary>
/// Behaviour covered for <c>TrainingSearchIndexEventHandlers</c>.
/// </summary>
public sealed class TrainingSearchIndexEventHandlersTests
{
    private readonly Mock<ITrainingSearchIndexer> _searchIndexer = new();

    /// <summary>
    /// Handle, training created, indexes the training.
    /// </summary>
    [Fact]
    public async Task Handle_TrainingCreated_IndexesTheTraining()
    {
        var trainingId = TrainingId.Generate();
        var trainerId = TrainerId.Generate();
        var sut = new IndexTrainingWhenTrainingCreatedEventHandler(_searchIndexer.Object);

        await sut.Handle(new TrainingCreatedDomainEvent(trainingId, trainerId), CancellationToken.None);

        _searchIndexer.Verify(
            s => s.IndexAsync(trainingId.Value, trainerId.Value, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Handle, training edited, reindexes the training.
    /// </summary>
    [Fact]
    public async Task Handle_TrainingEdited_ReindexesTheTraining()
    {
        var trainingId = TrainingId.Generate();
        var trainerId = TrainerId.Generate();
        var sut = new ReindexTrainingWhenTrainingEditedEventHandler(_searchIndexer.Object);

        await sut.Handle(new TrainingEditedDomainEvent(trainingId, trainerId), CancellationToken.None);

        _searchIndexer.Verify(
            s => s.IndexAsync(trainingId.Value, trainerId.Value, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
