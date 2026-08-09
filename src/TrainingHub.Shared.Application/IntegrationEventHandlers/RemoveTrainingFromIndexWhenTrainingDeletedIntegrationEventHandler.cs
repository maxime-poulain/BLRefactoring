using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Search;

namespace TrainingHub.Shared.Application.IntegrationEventHandlers;

/// <summary>
/// Takes a deleted training out of the read-side search index, from the committed fact.
/// </summary>
/// <remarks>
/// The consumer that closes the hotspot the event storming boards carried from the day they were
/// written: a training created, indexed and then deleted used to stay in the index for ever,
/// because deleting announced nothing and the port could not express a removal. A separate consumer
/// from the withdrawal's, though both call the same operation — they answer different facts, and
/// merging them would mean one delivery ledger entry for two things that can happen to the same
/// training.
/// </remarks>
public sealed class RemoveTrainingFromIndexWhenTrainingDeletedIntegrationEventHandler(
    ITrainingSearchIndexer searchIndexer)
    : IIntegrationEventHandler<TrainingDeletedIntegrationEvent>
{
    /// <inheritdoc />
    public string ConsumerName => "RemoveDeletedTrainingFromIndex";

    /// <summary>
    /// Runs the reaction to a delivered fact.
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task HandleAsync(TrainingDeletedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        await searchIndexer.RemoveAsync(integrationEvent.TrainingId, cancellationToken);
}
