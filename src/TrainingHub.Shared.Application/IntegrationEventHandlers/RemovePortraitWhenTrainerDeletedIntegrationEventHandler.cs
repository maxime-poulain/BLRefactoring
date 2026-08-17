using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;

namespace TrainingHub.Shared.Application.IntegrationEventHandlers;

/// <summary>
/// Collects an erased trainer's portrait: deletes the bytes the erasing transaction could not.
/// </summary>
/// <remarks>
/// The bytes live in an object store no transaction reaches, so they cannot die inside the
/// erasure (a rollback could not put them back — ADR 0021's ordering), and deleting them inline
/// after the commit would hang the promise of erasure on a request thread a crash can cut short.
/// Here the outbox retries what an inline call would lose (ADR 0085). The photo's identity comes
/// off the fact, because by the time this runs no table can answer it; a trainer who had no
/// portrait publishes the fact with none, and this consumer does nothing. Deleting bytes that
/// are already gone is a no-op at the store, which is what makes redelivery safe (ADR 0034).
/// </remarks>
public sealed class RemovePortraitWhenTrainerDeletedIntegrationEventHandler(ITrainerPhotoStore photoStore)
    : IIntegrationEventHandler<TrainerDeletedIntegrationEvent>
{
    /// <inheritdoc />
    public string ConsumerName => "RemoveErasedPortrait";

    /// <summary>
    /// Runs the reaction to a delivered fact.
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task HandleAsync(
        TrainerDeletedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.PhotoId is not { } photoId)
        {
            return;
        }

        await photoStore.DeleteAsync(
            TrainerId.Create(integrationEvent.TrainerId),
            PhotoId.Create(photoId),
            cancellationToken);
    }
}
