using BLRefactoring.Shared.Common.Specifications;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.Specifications;

/// <summary>
/// Specification that checks whether a training with a given title already exists for a specific trainer.
/// Used to enforce title uniqueness per trainer.
/// </summary>
public sealed class TrainingTitleExistsForTrainerSpecification : Specification<Training>
{
    /// <summary>
    /// Initializes a new instance of <see cref="TrainingTitleExistsForTrainerSpecification"/>
    /// that matches trainings with the specified title belonging to the specified trainer.
    /// </summary>
    /// <param name="title">The training title to check for.</param>
    /// <param name="trainerId">The identifier of the trainer.</param>
    public TrainingTitleExistsForTrainerSpecification(TrainingTitle title, TrainerId trainerId)
    {
        AddCriteria(training => training.Title == title && training.TrainerId == trainerId);
    }
}
