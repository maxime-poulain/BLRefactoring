using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.Specifications;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Specifications;

/// <summary>
/// Unit tests for <see cref="TrainingsByTrainerSpecification"/>.
/// Validates that the specification correctly filters trainings by trainer identifier.
/// </summary>
public class TrainingsByTrainerSpecificationTests
{
    /// <summary>
    /// Verifies that only trainings belonging to the specified trainer are returned.
    /// </summary>
    [Fact]
    public async Task Criteria_FiltersTrainingsByTrainerId()
    {
        // Arrange
        var targetTrainerId = Guid.NewGuid();
        var otherTrainerId = Guid.NewGuid();

        var training1 = await new TrainingBuilder().WithTrainerId(targetTrainerId).BuildValidAsync();
        var training2 = await new TrainingBuilder().WithTrainerId(targetTrainerId).WithTitle("Another Valid Title").BuildValidAsync();
        var training3 = await new TrainingBuilder().WithTrainerId(otherTrainerId).BuildValidAsync();

        var trainings = new[] { training1, training2, training3 };

        var spec = new TrainingsByTrainerSpecification((TrainerId)(Guid)targetTrainerId);

        // Act
        var result = trainings.AsQueryable().Where(spec.Criteria!.Compile()).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(training1);
        result.Should().Contain(training2);
    }
}
