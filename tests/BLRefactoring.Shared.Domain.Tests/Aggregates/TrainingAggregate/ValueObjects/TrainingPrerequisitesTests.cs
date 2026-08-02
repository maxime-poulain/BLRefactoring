using AwesomeAssertions;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Aggregates.TrainingAggregate.ValueObjects;

public sealed class TrainingPrerequisitesTests
{
    [Fact]
    public void Create_ValidPrerequisites_ReturnsSuccess()
    {
        // Act
        var result = TrainingPrerequisites.Create("Basic programming knowledge required.");

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Create_ValidPrerequisites_TrimsWhitespace()
    {
        // Act
        var prerequisites = TrainingPrerequisites.Create("  Basic knowledge  ").ShouldBeSuccess();

        // Assert
        prerequisites.Value.Should().Be("Basic knowledge");
    }

    [Fact]
    public void Create_Null_ReturnsFailure()
    {
        // Act
        var result = TrainingPrerequisites.Create(null!);

        // Assert
        result.ShouldContainError(TrainingErrorCodes.InvalidPrerequisites);
    }

    [Fact]
    public void Create_Empty_ReturnsFailure()
    {
        // Act
        var result = TrainingPrerequisites.Create(string.Empty);

        // Assert
        result.ShouldContainError(TrainingErrorCodes.InvalidPrerequisites);
    }

    [Fact]
    public void Create_ExactlyMaxLength_ReturnsSuccess()
    {
        // Arrange
        var prerequisites = new string('a', 500);

        // Act
        var result = TrainingPrerequisites.Create(prerequisites);

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Create_ExceedsMaxLength_ReturnsFailure()
    {
        // Arrange
        var prerequisites = new string('a', 501);

        // Act
        var result = TrainingPrerequisites.Create(prerequisites);

        // Assert
        result.ShouldContainError(TrainingErrorCodes.InvalidPrerequisites);
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        // Arrange
        var prereq1 = TrainingPrerequisites.Create("Same prerequisites").ShouldBeSuccess();
        var prereq2 = TrainingPrerequisites.Create("Same prerequisites").ShouldBeSuccess();

        // Assert
        prereq1.Should().Be(prereq2);
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        // Arrange
        var prereq1 = TrainingPrerequisites.Create("First prerequisites").ShouldBeSuccess();
        var prereq2 = TrainingPrerequisites.Create("Second prerequisites").ShouldBeSuccess();

        // Assert
        prereq1.Should().NotBe(prereq2);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var prerequisites = TrainingPrerequisites.Create("Basic knowledge").ShouldBeSuccess();

        // Assert
        prerequisites.ToString().Should().Be("Basic knowledge");
    }
}
