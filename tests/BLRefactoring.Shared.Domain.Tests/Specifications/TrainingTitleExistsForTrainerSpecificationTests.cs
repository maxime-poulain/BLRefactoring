using AwesomeAssertions;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.Specifications;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Specifications;

/// <summary>
/// Unit tests for <see cref="TrainingTitleExistsForTrainerSpecification"/>.
/// Validates that the specification correctly matches trainings by title and trainer.
/// </summary>
public sealed class TrainingTitleExistsForTrainerSpecificationTests
{
    /// <summary>
    /// Verifies that the specification matches a training with the exact title and trainer.
    /// </summary>
    [Fact]
    public async Task Criteria_MatchesTitleAndTrainer()
    {
        // Arrange
        var trainerId = Guid.NewGuid();
        var training = await new TrainingBuilder()
            .WithTitle("Valid Training Title")
            .WithTrainerId(trainerId)
            .BuildValidAsync();

        var trainings = new[] { training };

        var title = TrainingTitle.Create("Valid Training Title").Match(t => t, _ => null!);
        var spec = new TrainingTitleExistsForTrainerSpecification(title, TrainerId.Create(trainerId));

        // Act
        var result = trainings.AsQueryable().Where(spec.Criteria!.Compile()).ToList();

        // Assert
        result.Should().HaveCount(1);
    }

    /// <summary>
    /// Verifies that the specification does not match when the trainer is different.
    /// </summary>
    [Fact]
    public async Task Criteria_DifferentTrainer_DoesNotMatch()
    {
        // Arrange
        var trainerId = Guid.NewGuid();
        var otherTrainerId = Guid.NewGuid();
        var training = await new TrainingBuilder()
            .WithTitle("Valid Training Title")
            .WithTrainerId(trainerId)
            .BuildValidAsync();

        var trainings = new[] { training };

        var title = TrainingTitle.Create("Valid Training Title").Match(t => t, _ => null!);
        var spec = new TrainingTitleExistsForTrainerSpecification(title, TrainerId.Create(otherTrainerId));

        // Act
        var result = trainings.AsQueryable().Where(spec.Criteria!.Compile()).ToList();

        // Assert
        result.Should().BeEmpty();
    }
}
