using AwesomeAssertions;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.Specifications;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Xunit;

namespace TrainingHub.Shared.Domain.Tests.Specifications;

/// <summary>
/// Behaviour covered for <see cref="TrainingOwnedBySpecification"/>: the ownership rule, asked
/// both ways.
/// </summary>
public sealed class TrainingOwnedBySpecificationTests
{
    /// <summary>
    /// The owner, satisfies the rule.
    /// </summary>
    [Fact]
    public async Task TheOwner_SatisfiesTheRule()
    {
        var ownerId = Guid.NewGuid();
        var training = await new TrainingBuilder().WithTrainerId(ownerId).BuildValidAsync();

        var satisfied = new TrainingOwnedBySpecification(TrainerId.Create(ownerId)).IsSatisfiedBy(training);

        satisfied.Should().BeTrue();
    }

    /// <summary>
    /// Anybody else, does not.
    /// </summary>
    [Fact]
    public async Task AnybodyElse_DoesNot()
    {
        var training = await new TrainingBuilder().WithTrainerId(Guid.NewGuid()).BuildValidAsync();

        var satisfied = new TrainingOwnedBySpecification(TrainerId.Create(Guid.NewGuid())).IsSatisfiedBy(training);

        satisfied.Should().BeFalse();
    }

    /// <summary>
    /// The aggregate, wears the rule.
    /// </summary>
    /// <remarks>
    /// <c>Training.IsOwnedBy</c> is the specification worn as a behaviour method; asking either
    /// must be asking the same rule, or the aggregate's surface and the domain's statement have
    /// drifted apart.
    /// </remarks>
    [Fact]
    public async Task TheAggregate_WearsTheRule()
    {
        var ownerId = Guid.NewGuid();
        var training = await new TrainingBuilder().WithTrainerId(ownerId).BuildValidAsync();

        training.IsOwnedBy(TrainerId.Create(ownerId)).Should().BeTrue();
        training.IsOwnedBy(TrainerId.Create(Guid.NewGuid())).Should().BeFalse();
    }

    /// <summary>
    /// Both evaluations, answer alike.
    /// </summary>
    /// <remarks>
    /// The point of one specification carrying one expression: what the database would be asked
    /// (<c>Criteria</c>) and what memory answers (<c>IsSatisfiedBy</c>) cannot disagree, because
    /// there is only one statement of the rule to read. This test is the claim, exercised.
    /// </remarks>
    [Fact]
    public async Task BothEvaluations_AnswerAlike()
    {
        var ownerId = Guid.NewGuid();
        var owned = await new TrainingBuilder().WithTrainerId(ownerId).BuildValidAsync();
        var foreign = await new TrainingBuilder().WithTrainerId(Guid.NewGuid()).BuildValidAsync();
        var spec = new TrainingOwnedBySpecification(TrainerId.Create(ownerId));

        var byCriteria = new[] { owned, foreign }.AsQueryable().Where(spec.Criteria).ToList();

        byCriteria.Should().ContainSingle().Which.Should().BeSameAs(owned);
        spec.IsSatisfiedBy(owned).Should().BeTrue();
        spec.IsSatisfiedBy(foreign).Should().BeFalse();
    }
}
