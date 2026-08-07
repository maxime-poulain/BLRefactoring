using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;

namespace TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;

/// <summary>
/// Raised when a <see cref="Training"/> has been withdrawn from public view by its owner.
/// </summary>
/// <remarks>
/// The everyday act ADR 0050 puts in the place deletion used to hold. The training keeps its title
/// and its rows; what leaves is its place in the catalogue's quota and its entry in the search
/// index.
/// </remarks>
/// <param name="TrainingId">The identifier of the withdrawn training.</param>
/// <param name="TrainerId">The identifier of the trainer owning the training.</param>
public sealed record TrainingUnpublishedDomainEvent(
    TrainingId TrainingId,
    TrainerId TrainerId) : IDomainEvent;
