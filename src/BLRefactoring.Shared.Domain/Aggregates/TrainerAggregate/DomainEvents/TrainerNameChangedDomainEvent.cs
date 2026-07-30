using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;

/// <summary>
/// Raised when a <see cref="Trainer"/>'s name has changed.
/// </summary>
/// <param name="TrainerId">The identifier of the trainer whose name changed.</param>
/// <param name="OldFirstname">The first name before the change.</param>
/// <param name="OldLastname">The last name before the change.</param>
/// <param name="NewFirstname">The first name after the change.</param>
/// <param name="NewLastname">The last name after the change.</param>
public sealed record TrainerNameChangedDomainEvent(
    TrainerId TrainerId,
    string OldFirstname,
    string OldLastname,
    string NewFirstname,
    string NewLastname) : IDomainEvent;
