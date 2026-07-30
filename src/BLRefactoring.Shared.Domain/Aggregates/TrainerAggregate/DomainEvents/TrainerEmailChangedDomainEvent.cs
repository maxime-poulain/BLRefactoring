using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;

/// <summary>
/// Raised when a <see cref="Trainer"/>'s email has changed.
/// </summary>
/// <param name="TrainerId">The identifier of the trainer whose email changed.</param>
/// <param name="OldEmail">The email address before the change.</param>
/// <param name="NewEmail">The email address after the change.</param>
public sealed record TrainerEmailChangedDomainEvent(
    TrainerId TrainerId,
    string OldEmail,
    string NewEmail) : IDomainEvent;
