using TrainingHub.Shared.Application.IntegrationEvents;

namespace TrainingHub.Shared.Application.IntegrationEventHandlers;

/// <summary>
/// Takes a withdrawn training out of the read-side search index, from the committed fact.
/// </summary>
/// <remarks>
/// This is what makes withdrawing observable to anybody but its owner. Without it the status would
/// be a field the write side respects and every reader ignores — which is precisely the soft delete
/// ADR 0050 refuses to be. Removing an entry that is not there is not an error, so a redelivered
/// message is harmless (ADR 0034).
/// </remarks>
public sealed class RemoveTrainingFromIndexWhenTrainingUnpublishedIntegrationEventHandler(
    ITrainingSearchIndexer searchIndexer)
    : IIntegrationEventHandler<TrainingUnpublishedIntegrationEvent>
{
    /// <inheritdoc />
    public string ConsumerName => "RemoveUnpublishedTrainingFromIndex";

    /// <summary>
    /// Runs the reaction to a delivered fact.
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task HandleAsync(TrainingUnpublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        await searchIndexer.RemoveAsync(integrationEvent.TrainingId, cancellationToken);
}
