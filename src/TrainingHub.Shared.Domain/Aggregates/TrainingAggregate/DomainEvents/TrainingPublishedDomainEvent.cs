using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;

namespace TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;

/// <summary>
/// Raised when a <see cref="Training"/> has been offered to the public.
/// </summary>
/// <remarks>
/// Raised by <see cref="Training.PublishAsync"/> only when the training was not already published:
/// a transition that changes nothing announces nothing.
/// </remarks>
/// <param name="TrainingId">The identifier of the published training.</param>
/// <param name="TrainerId">The identifier of the trainer owning the training.</param>
public sealed record TrainingPublishedDomainEvent(
    TrainingId TrainingId,
    TrainerId TrainerId) : IDomainEvent;
