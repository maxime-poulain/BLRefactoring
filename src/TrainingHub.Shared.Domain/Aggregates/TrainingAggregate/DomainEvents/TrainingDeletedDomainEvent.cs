using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;

namespace TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;

/// <summary>
/// Raised when a <see cref="Training"/> has been marked for deletion.
/// </summary>
/// <remarks>
/// The fact deletion never announced. Its absence is what left an indexed training in the index for
/// ever once its rows were gone — a hotspot the event storming boards carried from the day they
/// were written, and the defect ADR 0050 closes.
/// </remarks>
/// <param name="TrainingId">The identifier of the deleted training.</param>
/// <param name="TrainerId">The identifier of the trainer that owned the training.</param>
public sealed record TrainingDeletedDomainEvent(
    TrainingId TrainingId,
    TrainerId TrainerId) : IDomainEvent;
