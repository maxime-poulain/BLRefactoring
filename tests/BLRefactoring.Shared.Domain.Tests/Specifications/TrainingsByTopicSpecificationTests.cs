using AwesomeAssertions;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.Specifications;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Specifications;

/// <summary>
/// Unit tests for <see cref="TrainingsByTopicSpecification"/>.
/// Validates that the specification correctly filters trainings by topic name.
/// </summary>
public class TrainingsByTopicSpecificationTests
{
    /// <summary>
    /// Verifies that trainings containing the specified topic are returned.
    /// </summary>
    [Fact]
    public async Task Criteria_FiltersTrainingsByTopic()
    {
        // Arrange
        var training1 = await new TrainingBuilder().WithTopics("Programming").BuildValidAsync();
        var training2 = await new TrainingBuilder().WithTitle("Design Training!!!!").WithTopics("Design").BuildValidAsync();
        var training3 = await new TrainingBuilder().WithTitle("Multi Topic Training").WithTopics("Programming", "Design").BuildValidAsync();

        var trainings = new[] { training1, training2, training3 };

        var spec = new TrainingsByTopicSpecification(Topic.Programming);

        // Act
        var result = trainings.AsQueryable().Where(spec.Criteria!.Compile()).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(training1);
        result.Should().Contain(training3);
    }

    /// <summary>
    /// Verifies that no trainings are returned when the topic does not match.
    /// </summary>
    [Fact]
    public async Task Criteria_NoMatch_ReturnsEmpty()
    {
        // Arrange
        var training1 = await new TrainingBuilder().WithTopics("Programming").BuildValidAsync();

        var trainings = new[] { training1 };

        var spec = new TrainingsByTopicSpecification(Topic.Marketing);

        // Act
        var result = trainings.AsQueryable().Where(spec.Criteria!.Compile()).ToList();

        // Assert
        result.Should().BeEmpty();
    }
}
