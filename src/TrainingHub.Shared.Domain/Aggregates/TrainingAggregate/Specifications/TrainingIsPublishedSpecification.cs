using System.Linq.Expressions;
using TrainingHub.Shared.Common.Specifications;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

namespace TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.Specifications;

/// <summary>
/// The training is on offer rather than withdrawn.
/// </summary>
/// <remarks>
/// The half of the catalogue-capacity rule that changed when withdrawing became possible.
/// <see cref="ITrainingCounter"/> counts a trainer's trainings, and what it must count is the ones
/// the public can see: an unpublished training holds no place in the quota, so its owner is not
/// locked out of their own catalogue by ten trainings nobody is being offered (ADR 0050).
/// <para>
/// One expression, answering in memory and as query criteria alike — the aggregate's rule and the
/// database's filter cannot drift apart because there is only one of them (ADR 0028).
/// </para>
/// </remarks>
public sealed class TrainingIsPublishedSpecification : Specification<Training>
{
    /// <summary>
    /// States the question.
    /// </summary>
    public TrainingIsPublishedSpecification()
        : base(Rule())
    {
    }

    private static Expression<Func<Training, bool>> Rule()
        => training => training.Status == TrainingStatus.Published;
}
