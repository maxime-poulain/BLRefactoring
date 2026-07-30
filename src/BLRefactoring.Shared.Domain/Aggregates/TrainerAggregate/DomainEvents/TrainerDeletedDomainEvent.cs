using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;

/// <summary>
/// Raised when a <see cref="Trainer"/> has been marked for deletion.
/// </summary>
/// <param name="TrainerId">The identifier of the deleted trainer.</param>
public sealed record TrainerDeletedDomainEvent(TrainerId TrainerId) : IDomainEvent;
