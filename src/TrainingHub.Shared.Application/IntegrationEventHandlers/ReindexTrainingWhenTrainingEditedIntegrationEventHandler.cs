using TrainingHub.Shared.Application.IntegrationEvents;

namespace TrainingHub.Shared.Application.IntegrationEventHandlers;

/// <summary>
/// Refreshes the search index entry of an edited training, from the committed fact.
/// </summary>
/// <remarks>
/// Counterpart of <see cref="IndexTrainingWhenTrainingCreatedIntegrationEventHandler"/> for edits.
/// The two facts stay distinct on the wire and the two reactions stay distinct here, for the same
/// reason as always: today both converge on the indexer's upsert, and the day an edit also
/// invalidates a cache or notifies subscribed students, only this file changes.
/// </remarks>
public sealed class ReindexTrainingWhenTrainingEditedIntegrationEventHandler(ITrainingSearchIndexer searchIndexer)
    : IIntegrationEventHandler<TrainingEditedIntegrationEvent>
{
    /// <inheritdoc />
    public string ConsumerName => "ReindexTraining";

    /// <summary>
    /// Runs the reaction to a delivered fact.
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task HandleAsync(TrainingEditedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        await searchIndexer.IndexAsync(
            integrationEvent.TrainingId,
            integrationEvent.TrainerId,
            cancellationToken);
}
