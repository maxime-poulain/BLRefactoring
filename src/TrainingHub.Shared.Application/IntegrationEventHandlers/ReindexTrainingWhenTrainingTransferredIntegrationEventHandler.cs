using TrainingHub.Shared.Application.IntegrationEvents;

namespace TrainingHub.Shared.Application.IntegrationEventHandlers;

/// <summary>
/// Moves a transferred training's search entry to its new owner, from the committed fact.
/// </summary>
/// <remarks>
/// Counterpart of <see cref="ReindexTrainingWhenTrainingEditedIntegrationEventHandler"/> for the
/// hand-over (ADR 0036): the indexer's upsert converges on the training's id, so re-indexing
/// under <c>NewTrainerId</c> is the whole move — the entry cannot exist twice. The delivery
/// ledger re-runs this at most across a lapsed lease (ADR 0034), and even then the upsert
/// converges.
/// </remarks>
public sealed class ReindexTrainingWhenTrainingTransferredIntegrationEventHandler(ITrainingSearchIndexer searchIndexer)
    : IIntegrationEventHandler<TrainingTransferredIntegrationEvent>
{
    /// <inheritdoc />
    public string ConsumerName => "ReindexTransferredTraining";

    /// <summary>
    /// Runs the reaction to a delivered fact.
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task HandleAsync(TrainingTransferredIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        await searchIndexer.IndexAsync(
            integrationEvent.TrainingId,
            integrationEvent.NewTrainerId,
            cancellationToken);
}
