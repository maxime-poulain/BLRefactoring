using BLRefactoring.Shared.Common.Specifications;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.Specifications;

/// <summary>
/// Specification that filters trainings by a given trainer identifier.
/// Matches all trainings belonging to the specified trainer.
/// </summary>
public sealed class TrainingsByTrainerSpecification : Specification<Training>
{
    /// <summary>
    /// Initializes a new instance of <see cref="TrainingsByTrainerSpecification"/>
    /// that matches trainings belonging to the specified trainer.
    /// </summary>
    /// <param name="trainerId">The identifier of the trainer to filter by.</param>
    public TrainingsByTrainerSpecification(TrainerId trainerId)
    {
        AddCriteria(training => training.TrainerId == trainerId);
    }
}
