using TrainingHub.Shared.Application.IntegrationEvents;

namespace TrainingHub.Shared.Application.IntegrationEventHandlers;

/// <summary>
/// Puts a republished training back into the read-side search index, from the committed fact.
/// </summary>
/// <remarks>
/// The mirror of the removal a withdrawal triggers, and what makes the pair a lifecycle rather
/// than a one-way door: a trainer who withdrew a training and thought better of it gets it back in
/// front of the public without recreating it. Redelivery costs nothing — indexing the same
/// identifiers twice lands on the same entry (ADR 0034).
/// </remarks>
public sealed class IndexTrainingWhenTrainingPublishedIntegrationEventHandler(ITrainingSearchIndexer searchIndexer)
    : IIntegrationEventHandler<TrainingPublishedIntegrationEvent>
{
    /// <inheritdoc />
    public string ConsumerName => "IndexPublishedTraining";

    /// <summary>
    /// Runs the reaction to a delivered fact.
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task HandleAsync(TrainingPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        await searchIndexer.IndexAsync(
            integrationEvent.TrainingId,
            integrationEvent.TrainerId,
            cancellationToken);
}
