using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Application.Search;

namespace TrainingHub.Shared.Application.IntegrationEventHandlers;

/// <summary>
/// Takes a withheld training out of the read-side search index, from the committed fact.
/// </summary>
/// <remarks>
/// The same removal a withdrawal causes, for a state that is not a withdrawal: what separates them
/// is who may undo it (ADR 0052), and the index has no interest in that — a training the public
/// must not be offered is a training the index must not serve, whoever took it down.
/// <para>
/// Nothing puts it back. A release lands the training on <c>Unpublished</c>, where it was not
/// indexed and nobody was waiting, which is why that half earns no fact at all.
/// </para>
/// </remarks>
public sealed class RemoveTrainingFromIndexWhenTrainingWithheldIntegrationEventHandler(
    ITrainingSearchIndexer searchIndexer)
    : IIntegrationEventHandler<TrainingWithheldIntegrationEvent>
{
    /// <inheritdoc />
    public string ConsumerName => "RemoveWithheldTrainingFromIndex";

    /// <summary>
    /// Runs the reaction to a delivered fact.
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task HandleAsync(TrainingWithheldIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        await searchIndexer.RemoveAsync(integrationEvent.TrainingId, cancellationToken);
}
