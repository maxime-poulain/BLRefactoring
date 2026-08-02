using AwesomeAssertions;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Aggregates.TrainingAggregate.ValueObjects;

/// <summary>
/// Behaviour covered for <c>TrainingDescription</c>.
/// </summary>
public sealed class TrainingDescriptionTests
{
    /// <summary>
    /// Create, valid description, returns success.
    /// </summary>
    [Fact]
    public void Create_ValidDescription_ReturnsSuccess()
    {
        // Act
        var result = TrainingDescription.Create("A valid training description for testing purposes.");

        // Assert
        result.ShouldBeSuccess();
    }

    /// <summary>
    /// Create, valid description, trims whitespace.
    /// </summary>
    [Fact]
    public void Create_ValidDescription_TrimsWhitespace()
    {
        // Act
        var description = TrainingDescription.Create("  A valid description  ").ShouldBeSuccess();

        // Assert
        description.Value.Should().Be("A valid description");
    }

    /// <summary>
    /// Create, null, returns failure.
    /// </summary>
    [Fact]
    public void Create_Null_ReturnsFailure()
    {
        // Act
        var result = TrainingDescription.Create(null!);

        // Assert
        result.ShouldContainError(TrainingErrorCodes.InvalidDescription);
    }

    /// <summary>
    /// Create, empty, returns failure.
    /// </summary>
    [Fact]
    public void Create_Empty_ReturnsFailure()
    {
        // Act
        var result = TrainingDescription.Create(string.Empty);

        // Assert
        result.ShouldContainError(TrainingErrorCodes.InvalidDescription);
    }

    /// <summary>
    /// Create, exactly max length, returns success.
    /// </summary>
    [Fact]
    public void Create_ExactlyMaxLength_ReturnsSuccess()
    {
        // Arrange
        var description = new string('a', 500);

        // Act
        var result = TrainingDescription.Create(description);

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
        var description = new string('a', 501);

        // Act
        var result = TrainingDescription.Create(description);

        // Assert
        result.ShouldContainError(TrainingErrorCodes.InvalidDescription);
    }

    /// <summary>
    /// Equality, same value, are equal.
    /// </summary>
    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        // Arrange
        var desc1 = TrainingDescription.Create("Same description").ShouldBeSuccess();
        var desc2 = TrainingDescription.Create("Same description").ShouldBeSuccess();

        // Assert
        desc1.Should().Be(desc2);
    }

    /// <summary>
    /// Equality, different value, are not equal.
    /// </summary>
    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        // Arrange
        var desc1 = TrainingDescription.Create("First description").ShouldBeSuccess();
        var desc2 = TrainingDescription.Create("Second description").ShouldBeSuccess();

        // Assert
        desc1.Should().NotBe(desc2);
    }

    /// <summary>
    /// To string, returns value.
    /// </summary>
    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var description = TrainingDescription.Create("A valid description").ShouldBeSuccess();

        // Assert
        description.ToString().Should().Be("A valid description");
    }
}
