using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

namespace TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;

/// <summary>
/// Raised when a <see cref="Trainer"/>'s name has changed.
/// </summary>
/// <param name="TrainerId">The identifier of the trainer whose name changed.</param>
/// <param name="OldName">The name before the change.</param>
/// <param name="NewName">The name after the change.</param>
public sealed record TrainerNameChangedDomainEvent(
    TrainerId TrainerId,
    Name OldName,
    Name NewName) : IDomainEvent;
