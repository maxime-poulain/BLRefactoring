namespace TrainingHub.Shared.Application.IntegrationEvents;

/// <summary>
/// A training was withdrawn from public view — the fact, as published to consumers outside this
/// bounded context.
/// </summary>
/// <remarks>
/// Carries the owner as well as the training, though the one consumer it has today needs only the
/// training: the fact is about a trainer's catalog changing shape, and a consumer that groups by
/// trainer should not have to ask who owned what.
/// </remarks>
/// <param name="TrainingId">The identifier of the withdrawn training.</param>
/// <param name="TrainerId">The identifier of the trainer who owns it.</param>
public sealed record TrainingUnpublishedIntegrationEvent(
    Guid TrainingId,
    Guid TrainerId) : IIntegrationEvent;
