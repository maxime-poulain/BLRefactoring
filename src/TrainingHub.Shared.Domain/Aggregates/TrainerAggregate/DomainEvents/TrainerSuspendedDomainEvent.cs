using TrainingHub.Shared.Common;

namespace TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;

/// <summary>
/// Raised when a <see cref="Trainer"/> has been placed under sanction.
/// </summary>
/// <remarks>
/// Carries no list of trainings, because the suspension touches none: the catalogue leaves public
/// view by being derived from its owner's standing. No integration event carries this outward yet —
/// no surface raises the sanction and no context consumes it, and building outbox plumbing for a
/// fact nobody produces is anticipation. See ADR 0050.
/// </remarks>
/// <param name="TrainerId">The identifier of the suspended trainer.</param>
public sealed record TrainerSuspendedDomainEvent(TrainerId TrainerId) : IDomainEvent;
