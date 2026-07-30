using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;

/// <summary>
/// Raised when a <see cref="Trainer"/> has been created.
/// </summary>
/// <param name="TrainerId">The identifier of the created trainer.</param>
public sealed record TrainerCreatedDomainEvent(TrainerId TrainerId) : IDomainEvent;
