using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;

namespace TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;

/// <summary>
/// Raised when a <see cref="Training"/> has been created.
/// </summary>
/// <param name="TrainingId">The identifier of the created training.</param>
/// <param name="TrainerId">The identifier of the trainer owning the training.</param>
public sealed record TrainingCreatedDomainEvent(
    TrainingId TrainingId,
    TrainerId TrainerId) : IDomainEvent;

/// <summary>
/// Raised when a <see cref="Training"/> has been edited.
/// </summary>
/// <param name="TrainingId">The identifier of the edited training.</param>
/// <param name="TrainerId">The identifier of the trainer owning the training.</param>
public sealed record TrainingEditedDomainEvent(
    TrainingId TrainingId,
    TrainerId TrainerId) : IDomainEvent;
