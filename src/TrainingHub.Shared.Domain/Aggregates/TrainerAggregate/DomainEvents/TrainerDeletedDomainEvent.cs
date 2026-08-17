using TrainingHub.Shared.Common;

namespace TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;

/// <summary>
/// Raised when a <see cref="Trainer"/> has been marked for deletion.
/// </summary>
/// <remarks>
/// Carries the portrait's identity beside the trainer's, and that is a design decision on the
/// event rather than redundancy: the bytes outlive the row that names them, so the policy that
/// has them removed (ADR 0085) needs an identity no table will be able to answer by the time it
/// runs. The precedent is the contact-email change carrying the old address — an event carries
/// what its policies need, resolved while the aggregate still knows it.
/// </remarks>
/// <param name="TrainerId">The identifier of the deleted trainer.</param>
/// <param name="PhotoId">The identifier of their portrait, when they had one.</param>
public sealed record TrainerDeletedDomainEvent(TrainerId TrainerId, PhotoId? PhotoId) : IDomainEvent;
