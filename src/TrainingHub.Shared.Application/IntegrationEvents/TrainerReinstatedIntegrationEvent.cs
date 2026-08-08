namespace TrainingHub.Shared.Application.IntegrationEvents;

/// <summary>
/// The sanction against a trainer was lifted — the fact, as published to consumers outside this
/// bounded context.
/// </summary>
/// <remarks>
/// An identifier and nothing else, because a lifting restores rather than decides: there is no
/// motive to carry, and the catalogue that comes back is the one that was never written to
/// (ADR 0050).
/// </remarks>
/// <param name="TrainerId">The identifier of the reinstated trainer.</param>
public sealed record TrainerReinstatedIntegrationEvent(Guid TrainerId) : IIntegrationEvent;
