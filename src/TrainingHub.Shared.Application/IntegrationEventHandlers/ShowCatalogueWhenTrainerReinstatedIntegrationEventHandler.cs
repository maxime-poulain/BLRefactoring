using TrainingHub.Shared.Application.IntegrationEvents;

namespace TrainingHub.Shared.Application.IntegrationEventHandlers;

/// <summary>
/// Puts a reinstated trainer's catalogue back into the read-side search index, from the committed
/// fact.
/// </summary>
/// <remarks>
/// Without this the sanction would be reversible in the write model and permanent everywhere a
/// visitor looks, which is the tombstone ADR 0050 refuses. It costs one call because its
/// counterpart hid the entries instead of deleting them: an index that had forgotten the catalogue
/// would have to be told it again, training by training, for a lifting that restores rather than
/// decides (ADR 0056).
/// </remarks>
public sealed class ShowCatalogueWhenTrainerReinstatedIntegrationEventHandler(
    ITrainingSearchIndexer searchIndexer)
    : IIntegrationEventHandler<TrainerReinstatedIntegrationEvent>
{
    /// <inheritdoc />
    public string ConsumerName => "ShowReinstatedTrainerCatalogue";

    /// <summary>
    /// Runs the reaction to a delivered fact.
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task HandleAsync(TrainerReinstatedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        await searchIndexer.ShowTrainerCatalogueAsync(integrationEvent.TrainerId, cancellationToken);
}
