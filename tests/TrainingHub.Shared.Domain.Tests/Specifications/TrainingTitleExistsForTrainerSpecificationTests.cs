using AwesomeAssertions;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.Specifications;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Xunit;

namespace TrainingHub.Shared.Domain.Tests.Specifications;

/// <summary>
/// Behavior covered for <see cref="TrainingTitleExistsForTrainerSpecification"/>: the data half
/// of the uniqueness invariant, answered in memory.
/// </summary>
public sealed class TrainingTitleExistsForTrainerSpecificationTests
{
    /// <summary>
    /// The same title for the same trainer, is matched.
    /// </summary>
    [Fact]
    public async Task TheSameTitleForTheSameTrainer_IsMatched()
    {
        var trainerId = Guid.NewGuid();
        var training = await new TrainingBuilder()
            .WithTitle("Valid Training Title")
            .WithTrainerId(trainerId)
            .BuildValidAsync();

        var title = TrainingTitle.Create("Valid Training Title").Match(t => t, _ => null!);
        var spec = new TrainingTitleExistsForTrainerSpecification(title, TrainerId.Create(trainerId));

        spec.IsSatisfiedBy(training).Should().BeTrue();
    }

    /// <summary>
    /// The same title for another trainer, is not.
    /// </summary>
    /// <remarks>
    /// The heart of the invariant's scope: uniqueness is per trainer, so two trainers may both
    /// list "Valid Training Title" and neither is in the other's way.
    /// </remarks>
    [Fact]
    public async Task TheSameTitleForAnotherTrainer_IsNot()
    {
        var training = await new TrainingBuilder()
            .WithTitle("Valid Training Title")
            .WithTrainerId(Guid.NewGuid())
            .BuildValidAsync();

        var title = TrainingTitle.Create("Valid Training Title").Match(t => t, _ => null!);
        var spec = new TrainingTitleExistsForTrainerSpecification(title, TrainerId.Create(Guid.NewGuid()));

        spec.IsSatisfiedBy(training).Should().BeFalse();
    }

    /// <summary>
    /// Another title for the same trainer, is not.
    /// </summary>
    [Fact]
    public async Task AnotherTitleForTheSameTrainer_IsNot()
    {
        var trainerId = Guid.NewGuid();
        var training = await new TrainingBuilder()
            .WithTitle("Valid Training Title")
            .WithTrainerId(trainerId)
            .BuildValidAsync();

        var title = TrainingTitle.Create("A Different Title Entirely").Match(t => t, _ => null!);
        var spec = new TrainingTitleExistsForTrainerSpecification(title, TrainerId.Create(trainerId));

        spec.IsSatisfiedBy(training).Should().BeFalse();
    }
}
