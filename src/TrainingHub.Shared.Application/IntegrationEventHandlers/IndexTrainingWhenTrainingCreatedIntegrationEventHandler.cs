using TrainingHub.Shared.Application.IntegrationEvents;

namespace TrainingHub.Shared.Application.IntegrationEventHandlers;

/// <summary>
/// Adds a created training to the read-side search index, from the committed fact.
/// </summary>
/// <remarks>
/// The index used to be written from inside the transaction and could end up holding a training
/// the database never accepted. Fed from the outbox, it only ever learns of trainings that exist —
/// eventually, which is what a derived read model is allowed to be. The indexer's upsert makes
/// redelivery harmless: indexing the same identifiers twice converges on the same entry, so
/// at-least-once costs nothing here.
/// </remarks>
public sealed class IndexTrainingWhenTrainingCreatedIntegrationEventHandler(ITrainingSearchIndexer searchIndexer)
    : IIntegrationEventHandler<TrainingCreatedIntegrationEvent>
{
    /// <summary>
    /// Runs the reaction to a delivered fact.
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task HandleAsync(TrainingCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        await searchIndexer.IndexAsync(
            integrationEvent.TrainingId,
            integrationEvent.TrainerId,
            cancellationToken);
}
