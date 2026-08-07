namespace TrainingHub.Shared.Application.IntegrationEvents;

/// <summary>
/// A training was offered to the public — the fact, as published to consumers outside this bounded
/// context.
/// </summary>
/// <remarks>
/// Identifiers only, like every fact this context announces: a consumer wanting the training's
/// content reads it through the API rather than through a message that would have to chase its
/// every subsequent edit.
/// </remarks>
/// <param name="TrainingId">The identifier of the published training.</param>
/// <param name="TrainerId">The identifier of the trainer who owns it.</param>
public sealed record TrainingPublishedIntegrationEvent(
    Guid TrainingId,
    Guid TrainerId) : IIntegrationEvent;
