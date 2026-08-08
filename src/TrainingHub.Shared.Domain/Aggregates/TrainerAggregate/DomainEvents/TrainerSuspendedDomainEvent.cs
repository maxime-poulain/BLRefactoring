using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

namespace TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;

/// <summary>
/// Raised when a <see cref="Trainer"/> has been placed under sanction.
/// </summary>
/// <remarks>
/// Carries no list of trainings, because the suspension touches none: the catalogue leaves public
/// view by being derived from its owner's standing. No integration event carries this outward yet —
/// no surface raises the sanction and no context consumes it, and building outbox plumbing for a
/// fact nobody produces is anticipation. See ADR 0050.
/// <para>
/// It carries the reason so that the audit handler can name it. Who acted and when are already
/// recoverable — ADR 0027's enricher stamps the caller on every line the request causes — and the
/// motive was the one part of the decision no log could recover (ADR 0052).
/// </para>
/// </remarks>
/// <param name="TrainerId">The identifier of the suspended trainer.</param>
/// <param name="Reason">Why they were suspended.</param>
public sealed record TrainerSuspendedDomainEvent(
    TrainerId TrainerId,
    SuspensionReason Reason) : IDomainEvent;
