using TrainingHub.Shared.Application.Search;
using TrainingHub.Shared.Application.IntegrationEventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using Moq;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.IntegrationEventHandlers;

/// <summary>
/// Behavior covered for the three consumers that keep the search index in step with a sanction.
/// </summary>
/// <remarks>
/// The two trainer facts are what makes ADR 0050's derived visibility observable: a suspension
/// writes to no training, so the index is told about the trainer once instead of about each
/// training in turn — and the pair being symmetric is what lets a lifting cost one call as well.
/// The third is a removal like any other, because an index has no interest in who took a training
/// down.
/// </remarks>
public sealed class SanctionIndexIntegrationEventHandlerTests
{
    private readonly Mock<ITrainingSearchIndexer> _searchIndexer = new();

    /// <summary>
    /// Handle, a suspension, hides the catalog in one call about the trainer.
    /// </summary>
    [Fact]
    public async Task Handle_ASuspension_HidesTheCatalogInOneCallAboutTheTrainer()
    {
        var trainerId = Guid.NewGuid();

        var sut = new HideCatalogWhenTrainerSuspendedIntegrationEventHandler(_searchIndexer.Object);
        await sut.HandleAsync(
            new TrainerSuspendedIntegrationEvent(trainerId, "Repeated breaches."), CancellationToken.None);

        _searchIndexer.Verify(
            indexer => indexer.HideTrainerCatalogAsync(trainerId, It.IsAny<CancellationToken>()),
            Times.Once);

        _searchIndexer.Verify(
            indexer => indexer.RemoveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "the sanction wrote to no training, so nothing is removed training by training");
    }

    /// <summary>
    /// Handle, a reinstatement, shows the catalog again.
    /// </summary>
    [Fact]
    public async Task Handle_AReinstatement_ShowsTheCatalogAgain()
    {
        var trainerId = Guid.NewGuid();

        var sut = new ShowCatalogWhenTrainerReinstatedIntegrationEventHandler(_searchIndexer.Object);
        await sut.HandleAsync(new TrainerReinstatedIntegrationEvent(trainerId), CancellationToken.None);

        _searchIndexer.Verify(
            indexer => indexer.ShowTrainerCatalogAsync(trainerId, It.IsAny<CancellationToken>()),
            Times.Once);

        _searchIndexer.Verify(
            indexer => indexer.IndexAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "nothing is indexed again, because nothing was forgotten");
    }

    /// <summary>
    /// Handle, a withholding, removes that training and nothing else.
    /// </summary>
    [Fact]
    public async Task Handle_AWithholding_RemovesThatTrainingAndNothingElse()
    {
        var trainingId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();

        var sut = new RemoveTrainingFromIndexWhenTrainingWithheldIntegrationEventHandler(_searchIndexer.Object);
        await sut.HandleAsync(
            new TrainingWithheldIntegrationEvent(trainingId, trainerId, "A training", "Misleading claims."),
            CancellationToken.None);

        _searchIndexer.Verify(
            indexer => indexer.RemoveAsync(trainingId, It.IsAny<CancellationToken>()),
            Times.Once);

        _searchIndexer.Verify(
            indexer => indexer.HideTrainerCatalogAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "one training was withheld, not its owner's whole catalog");
    }
}
