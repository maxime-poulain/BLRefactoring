namespace TrainingHub.Shared.Application.IntegrationEvents;

/// <summary>
/// A training was created — the fact, as published to consumers outside this bounded context.
/// </summary>
/// <remarks>
/// Identifiers only: the search indexer this fact currently feeds asks for nothing more, and a
/// consumer wanting details reads them through the API rather than through a message that would
/// otherwise have to chase the training's every subsequent edit.
/// </remarks>
/// <param name="TrainingId">The identifier of the created training.</param>
/// <param name="TrainerId">The identifier of the trainer who owns it.</param>
public sealed record TrainingCreatedIntegrationEvent(
    Guid TrainingId,
    Guid TrainerId) : IIntegrationEvent;
