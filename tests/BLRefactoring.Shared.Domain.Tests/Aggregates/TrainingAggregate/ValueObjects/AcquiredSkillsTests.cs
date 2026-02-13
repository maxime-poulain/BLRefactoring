using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Aggregates.TrainingAggregate.ValueObjects;

public class AcquiredSkillsTests
{
    [Fact]
    public void Create_ValidAcquiredSkills_ReturnsSuccess()
    {
        // Act
        var result = AcquiredSkills.Create("Advanced design patterns mastery.");

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Create_ValidAcquiredSkills_TrimsWhitespace()
    {
        // Act
        var skills = AcquiredSkills.Create("  Advanced patterns  ").ShouldBeSuccess();

        // Assert
        skills.Value.Should().Be("Advanced patterns");
    }

    [Fact]
    public void Create_Null_ReturnsFailure()
    {
        // Act
        var result = AcquiredSkills.Create(null!);

        // Assert
        result.ShouldContainError(ErrorCode.InvalidAcquiredSkills);
    }

    [Fact]
    public void Create_Empty_ReturnsFailure()
    {
        // Act
        var result = AcquiredSkills.Create(string.Empty);

        // Assert
        result.ShouldContainError(ErrorCode.InvalidAcquiredSkills);
    }

    [Fact]
    public void Create_ExactlyMaxLength_ReturnsSuccess()
    {
        // Arrange
        var skills = new string('a', 500);

        // Act
        var result = AcquiredSkills.Create(skills);

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Create_ExceedsMaxLength_ReturnsFailure()
    {
        // Arrange
        var skills = new string('a', 501);

        // Act
        var result = AcquiredSkills.Create(skills);

        // Assert
        result.ShouldContainError(ErrorCode.InvalidAcquiredSkills);
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        // Arrange
        var skills1 = AcquiredSkills.Create("Same skills description").ShouldBeSuccess();
        var skills2 = AcquiredSkills.Create("Same skills description").ShouldBeSuccess();

        // Assert
        skills1.Should().Be(skills2);
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        // Arrange
        var skills1 = AcquiredSkills.Create("First skills description").ShouldBeSuccess();
        var skills2 = AcquiredSkills.Create("Second skills description").ShouldBeSuccess();

        // Assert
        skills1.Should().NotBe(skills2);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var skills = AcquiredSkills.Create("Advanced patterns").ShouldBeSuccess();

        // Assert
        skills.ToString().Should().Be("Advanced patterns");
    }
}
