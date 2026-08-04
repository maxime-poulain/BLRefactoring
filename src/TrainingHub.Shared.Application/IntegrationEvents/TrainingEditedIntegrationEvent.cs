namespace TrainingHub.Shared.Application.IntegrationEvents;

/// <summary>
/// A training was edited — the fact, as published to consumers outside this bounded context.
/// </summary>
/// <remarks>
/// Kept apart from <see cref="TrainingCreatedIntegrationEvent"/> even though today's only consumer
/// treats both as "refresh the index": they are different business facts, and a consumer that cares
/// about the difference — a cache to invalidate, subscribed students to notify — must not have to
/// guess it back out of a merged message.
/// </remarks>
/// <param name="TrainingId">The identifier of the edited training.</param>
/// <param name="TrainerId">The identifier of the trainer who owns it.</param>
public sealed record TrainingEditedIntegrationEvent(
    Guid TrainingId,
    Guid TrainerId) : IIntegrationEvent;
