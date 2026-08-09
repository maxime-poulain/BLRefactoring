using AwesomeAssertions;
using Moq;
using TrainingHub.Shared.Application.IntegrationEventHandlers;
using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Search;
using Xunit;

namespace TrainingHub.DDD.Application.Tests.IntegrationEventHandlers;

/// <summary>
/// Behaviour covered for the three consumers that keep the search index in step with a training's
/// own lifecycle.
/// </summary>
/// <remarks>
/// They went untested for as long as the index was a fake that logged, where a wrong call was a
/// wrong log line. They are now the only three roads by which a published training reaches the
/// public catalogue and leaves it again (ADR 0059).
/// <para>
/// Each fact asserts the call that should happen <em>and</em> that the opposite one did not, for the
/// reason ADR 0056 gives about its own pair: a consumer that removed where it should have indexed
/// would leave the catalogue right in the end and be wrong about everything on the way.
/// </para>
/// </remarks>
public sealed class TrainingLifecycleIndexIntegrationEventHandlerTests
{
    private readonly Mock<ITrainingSearchIndexer> _searchIndexer = new();
    private readonly Guid _trainingId = Guid.NewGuid();
    private readonly Guid _trainerId = Guid.NewGuid();

    /// <summary>
    /// Handle, a publication, indexes the training and removes nothing.
    /// </summary>
    /// <remarks>
    /// The mirror of the withdrawal below, and what makes the pair a lifecycle rather than a one-way
    /// door: a trainer who withdrew a training and thought better of it gets it back in front of the
    /// public without recreating it.
    /// </remarks>
    [Fact]
    public async Task HandleAsync_APublication_IndexesTheTrainingAndRemovesNothing()
    {
        var sut = new IndexTrainingWhenTrainingPublishedIntegrationEventHandler(_searchIndexer.Object);

        await sut.HandleAsync(
            new TrainingPublishedIntegrationEvent(_trainingId, _trainerId),
            CancellationToken.None);

        _searchIndexer.Verify(
            indexer => indexer.IndexAsync(_trainingId, _trainerId, It.IsAny<CancellationToken>()),
            Times.Once);
        _searchIndexer.Verify(
            indexer => indexer.RemoveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Handle, a withdrawal, removes the training and indexes nothing.
    /// </summary>
    /// <remarks>
    /// An unpublished training is one the public must not be offered, and an index that still served
    /// it would make the state a lie (ADR 0050).
    /// </remarks>
    [Fact]
    public async Task HandleAsync_AWithdrawal_RemovesTheTrainingAndIndexesNothing()
    {
        var sut = new RemoveTrainingFromIndexWhenTrainingUnpublishedIntegrationEventHandler(
            _searchIndexer.Object);

        await sut.HandleAsync(
            new TrainingUnpublishedIntegrationEvent(_trainingId, _trainerId),
            CancellationToken.None);

        _searchIndexer.Verify(
            indexer => indexer.RemoveAsync(_trainingId, It.IsAny<CancellationToken>()),
            Times.Once);
        _searchIndexer.Verify(
            indexer => indexer.IndexAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Handle, a deletion, removes the training and indexes nothing.
    /// </summary>
    /// <remarks>
    /// The consumer that closes the hotspot the event storming boards carried from the day they were
    /// written: a deleted training that stayed indexed for ever, since nothing announced the
    /// deletion either.
    /// </remarks>
    [Fact]
    public async Task HandleAsync_ADeletion_RemovesTheTrainingAndIndexesNothing()
    {
        var sut = new RemoveTrainingFromIndexWhenTrainingDeletedIntegrationEventHandler(
            _searchIndexer.Object);

        await sut.HandleAsync(
            new TrainingDeletedIntegrationEvent(_trainingId, _trainerId),
            CancellationToken.None);

        _searchIndexer.Verify(
            indexer => indexer.RemoveAsync(_trainingId, It.IsAny<CancellationToken>()),
            Times.Once);
        _searchIndexer.Verify(
            indexer => indexer.IndexAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// The withdrawal and the deletion, keep separate ledger identities.
    /// </summary>
    /// <remarks>
    /// Both call the same operation, and merging them would look like a simplification. It would
    /// cost one delivery ledger entry for two things that can happen to the same training
    /// (ADR 0034), and the index's only way of telling a withdrawal from a deletion — the
    /// distinction a consumer that one day keeps a tombstone must not have to guess back (ADR 0050).
    /// </remarks>
    [Fact]
    public void TheWithdrawalAndTheDeletion_KeepSeparateLedgerIdentities()
    {
        var withdrawal = new RemoveTrainingFromIndexWhenTrainingUnpublishedIntegrationEventHandler(
            _searchIndexer.Object);
        var deletion = new RemoveTrainingFromIndexWhenTrainingDeletedIntegrationEventHandler(
            _searchIndexer.Object);

        withdrawal.ConsumerName.Should().NotBe(deletion.ConsumerName);
    }
}
