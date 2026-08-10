using AwesomeAssertions;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Xunit;

namespace TrainingHub.Shared.Domain.Tests.Aggregates.TrainingAggregate.ValueObjects;

/// <summary>
/// Behavior covered for <c>AcquiredSkills</c>.
/// </summary>
public sealed class AcquiredSkillsTests
{
    /// <summary>
    /// Create, valid acquired skills, returns success.
    /// </summary>
    [Fact]
    public void Create_ValidAcquiredSkills_ReturnsSuccess()
    {
        // Act
        var result = AcquiredSkills.Create("Advanced design patterns mastery.");

        // Assert
        result.ShouldBeSuccess();
    }

    /// <summary>
    /// Create, valid acquired skills, trims whitespace.
    /// </summary>
    [Fact]
    public void Create_ValidAcquiredSkills_TrimsWhitespace()
    {
        // Act
        var skills = AcquiredSkills.Create("  Advanced patterns  ").ShouldBeSuccess();

        // Assert
        skills.Value.Should().Be("Advanced patterns");
    }

    /// <summary>
    /// Create, null, returns failure.
    /// </summary>
    [Fact]
    public void Create_Null_ReturnsFailure()
    {
        // Act
        var result = AcquiredSkills.Create(null!);

        // Assert
        result.ShouldContainError(TrainingErrorCodes.InvalidAcquiredSkills);
    }

    /// <summary>
    /// Create, empty, returns failure.
    /// </summary>
    [Fact]
    public void Create_Empty_ReturnsFailure()
    {
        // Act
        var result = AcquiredSkills.Create(string.Empty);

        // Assert
        result.ShouldContainError(TrainingErrorCodes.InvalidAcquiredSkills);
    }

    /// <summary>
    /// Create, exactly max length, returns success.
    /// </summary>
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

    /// <summary>
    /// Create, exceeds max length, returns failure.
    /// </summary>
    [Fact]
    public void Create_ExceedsMaxLength_ReturnsFailure()
    {
        // Arrange
        var skills = new string('a', 501);

        // Act
        var result = AcquiredSkills.Create(skills);

        // Assert
        result.ShouldContainError(TrainingErrorCodes.InvalidAcquiredSkills);
    }

    /// <summary>
    /// Equality, same value, are equal.
    /// </summary>
    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        // Arrange
        var skills1 = AcquiredSkills.Create("Same skills description").ShouldBeSuccess();
        var skills2 = AcquiredSkills.Create("Same skills description").ShouldBeSuccess();

        // Assert
        skills1.Should().Be(skills2);
    }

    /// <summary>
    /// Equality, different value, are not equal.
    /// </summary>
    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        // Arrange
        var skills1 = AcquiredSkills.Create("First skills description").ShouldBeSuccess();
        var skills2 = AcquiredSkills.Create("Second skills description").ShouldBeSuccess();

        // Assert
        skills1.Should().NotBe(skills2);
    }

    /// <summary>
    /// To string, returns value.
    /// </summary>
    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var skills = AcquiredSkills.Create("Advanced patterns").ShouldBeSuccess();

        // Assert
        skills.ToString().Should().Be("Advanced patterns");
    }
}
