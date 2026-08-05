namespace TrainingHub.Shared.Application.IntegrationEvents;

/// <summary>
/// A training changed hands — the fact, as published to consumers outside this bounded context.
/// </summary>
/// <remarks>
/// Carries both owners, because a consumer reacting to the transfer — the index moving the entry,
/// a future notification to the former owner — must not have to remember who used to hold the
/// training: by delivery time, nothing else does (ADR 0036).
/// </remarks>
/// <param name="TrainingId">The identifier of the transferred training.</param>
/// <param name="FormerTrainerId">The trainer who handed the training over.</param>
/// <param name="NewTrainerId">The trainer who received it.</param>
public sealed record TrainingTransferredIntegrationEvent(
    Guid TrainingId,
    Guid FormerTrainerId,
    Guid NewTrainerId) : IIntegrationEvent;
