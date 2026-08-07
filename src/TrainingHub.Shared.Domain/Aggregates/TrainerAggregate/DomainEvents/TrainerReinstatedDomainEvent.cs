using TrainingHub.Shared.Common;

namespace TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;

/// <summary>
/// Raised when the sanction against a <see cref="Trainer"/> has been lifted.
/// </summary>
/// <remarks>
/// The half that makes the suspension a state rather than a tombstone. The trainer's catalogue
/// returns exactly as it was, because nothing was written to it on the way in.
/// </remarks>
/// <param name="TrainerId">The identifier of the reinstated trainer.</param>
public sealed record TrainerReinstatedDomainEvent(TrainerId TrainerId) : IDomainEvent;
