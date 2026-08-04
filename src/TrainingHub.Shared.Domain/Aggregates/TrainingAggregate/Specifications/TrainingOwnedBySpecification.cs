using System.Linq.Expressions;
using TrainingHub.Shared.Common.Specifications;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;

namespace TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.Specifications;

/// <summary>
/// A training answers only to the trainer that published it.
/// </summary>
/// <remarks>
/// The one sentence this system says most often, finally said once. It was written three ways in
/// three places — the HTTP ownership policy comparing a claim, the layered use case comparing
/// identifiers inline, the CQRS readers scoping their SQL — and the middle one was a business
/// statement with no name. This specification is that name; the write side wears it through
/// <see cref="Training.IsOwnedBy"/>. The other two forms stay deliberately: the policy is
/// authorization and answers before anything loads, and the readers scope data rather than decide
/// anything — neither is the domain speaking.
/// </remarks>
public sealed class TrainingOwnedBySpecification : Specification<Training>
{
    /// <summary>
    /// States the rule for one owner.
    /// </summary>
    /// <param name="owner">The trainer the rule is asked about.</param>
    public TrainingOwnedBySpecification(TrainerId owner) : base(Rule(owner))
    {
    }

    private static Expression<Func<Training, bool>> Rule(TrainerId owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        return training => training.TrainerId == owner;
    }
}
