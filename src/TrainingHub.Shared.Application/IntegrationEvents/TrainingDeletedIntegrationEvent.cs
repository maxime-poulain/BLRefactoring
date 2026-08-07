namespace TrainingHub.Shared.Application.IntegrationEvents;

/// <summary>
/// A training was deleted — the fact, as published to consumers outside this bounded context.
/// </summary>
/// <remarks>
/// The fact deletion never announced, and the reason an indexed training used to outlive its own
/// rows. A consumer receiving this cannot read the training back through the API — it is gone — so
/// what the message carries has to be enough on its own, which for the entry being removed it is.
/// </remarks>
/// <param name="TrainingId">The identifier of the deleted training.</param>
/// <param name="TrainerId">The identifier of the trainer who owned it.</param>
public sealed record TrainingDeletedIntegrationEvent(
    Guid TrainingId,
    Guid TrainerId) : IIntegrationEvent;
