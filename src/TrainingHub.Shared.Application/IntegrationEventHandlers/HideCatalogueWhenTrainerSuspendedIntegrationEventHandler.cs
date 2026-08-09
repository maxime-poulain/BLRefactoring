using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Search;

namespace TrainingHub.Shared.Application.IntegrationEventHandlers;

/// <summary>
/// Takes a suspended trainer's catalogue out of the read-side search index, from the committed
/// fact.
/// </summary>
/// <remarks>
/// This is what makes the sanction observable to anybody but the trainer. Without it the standing
/// would be a column the write side respects and every reader ignores — the same objection ADR 0050
/// raised against a soft delete, arriving one aggregate higher.
/// <para>
/// One call about a trainer, never one per training: the suspension wrote to none of them, and the
/// index composes public visibility from the two states exactly as the domain does. That is also
/// what lets the reinstatement cost a single call rather than a rebuild (ADR 0056).
/// </para>
/// </remarks>
public sealed class HideCatalogueWhenTrainerSuspendedIntegrationEventHandler(
    ITrainingSearchIndexer searchIndexer)
    : IIntegrationEventHandler<TrainerSuspendedIntegrationEvent>
{
    /// <inheritdoc />
    public string ConsumerName => "HideSuspendedTrainerCatalogue";

    /// <summary>
    /// Runs the reaction to a delivered fact.
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task HandleAsync(TrainerSuspendedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        await searchIndexer.HideTrainerCatalogueAsync(integrationEvent.TrainerId, cancellationToken);
}
