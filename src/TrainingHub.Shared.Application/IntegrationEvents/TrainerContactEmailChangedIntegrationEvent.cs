namespace TrainingHub.Shared.Application.IntegrationEvents;

/// <summary>
/// A trainer's contact address changed — the fact, as published to consumers outside this bounded
/// context.
/// </summary>
/// <remarks>
/// Both addresses travel with the fact because the reaction that motivates it — warning the address
/// being left behind, once the delivery worker exists — needs the old one, and the aggregate has
/// already forgotten it by the time any consumer runs.
/// </remarks>
/// <param name="TrainerId">The identifier of the trainer whose contact address changed.</param>
/// <param name="OldContactEmail">The address the trainer moved away from.</param>
/// <param name="NewContactEmail">The address the trainer moved to.</param>
public sealed record TrainerContactEmailChangedIntegrationEvent(
    Guid TrainerId,
    string OldContactEmail,
    string NewContactEmail) : IIntegrationEvent;
