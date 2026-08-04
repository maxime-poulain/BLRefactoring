using System.Linq.Expressions;
using TrainingHub.Shared.Common.Specifications;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

namespace TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.Specifications;

/// <summary>
/// A training already claims this title for this trainer.
/// </summary>
/// <remarks>
/// The data half of the uniqueness invariant. The decision itself — refuse the edition, name the
/// error — belongs to <see cref="Training"/> and stays there; what the aggregate cannot do alone
/// is look at its siblings, so it asks through <see cref="IUniquenessTitleChecker"/>, and this
/// specification is the question the checker's implementation puts to the database. Rule in the
/// aggregate, criteria in the specification: neither restates the other.
/// </remarks>
public sealed class TrainingTitleExistsForTrainerSpecification : Specification<Training>
{
    /// <summary>
    /// States the question for one title and one trainer.
    /// </summary>
    /// <param name="title">The title being claimed.</param>
    /// <param name="trainerId">The trainer claiming it.</param>
    public TrainingTitleExistsForTrainerSpecification(TrainingTitle title, TrainerId trainerId)
        : base(Rule(title, trainerId))
    {
    }

    private static Expression<Func<Training, bool>> Rule(TrainingTitle title, TrainerId trainerId)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(trainerId);

        return training => training.Title == title && training.TrainerId == trainerId;
    }
}
