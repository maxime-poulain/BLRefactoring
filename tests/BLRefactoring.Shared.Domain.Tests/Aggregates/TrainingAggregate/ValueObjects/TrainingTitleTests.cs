using AwesomeAssertions;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Aggregates.TrainingAggregate.ValueObjects;

public sealed class TrainingTitleTests
{
    [Fact]
    public void Create_ValidTitle_ReturnsSuccess()
    {
        // Act
        var result = TrainingTitle.Create("Valid Title");

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Create_ValidTitle_TrimsWhitespace()
    {
        // Act
        var title = TrainingTitle.Create("  Valid Title  ").ShouldBeSuccess();

        // Assert
        title.Value.Should().Be("Valid Title");
    }

    [Fact]
    public void Create_NullTitle_ReturnsFailure()
    {
        // Act
        var result = TrainingTitle.Create(null!);

        // Assert
        result.ShouldContainError(TrainingErrorCodes.InvalidTitle);
    }

    [Fact]
    public void Create_EmptyTitle_ReturnsFailure()
    {
        // Act
        var result = TrainingTitle.Create(string.Empty);

        // Assert
        result.ShouldContainError(TrainingErrorCodes.InvalidTitle);
    }

    [Fact]
    public void Create_TooShort_ReturnsFailure()
    {
        // Act
        var result = TrainingTitle.Create("Abcd");

        // Assert
        result.ShouldContainError(TrainingErrorCodes.InvalidTitle);
    }

    [Fact]
    public void Create_ExactlyMinLength_ReturnsSuccess()
    {
        // Act
        var result = TrainingTitle.Create("Abcde");

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Create_ExactlyMaxLength_ReturnsSuccess()
    {
        // Arrange
        var title = new string('a', 100);

        // Act
        var result = TrainingTitle.Create(title);

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Create_TooLong_ReturnsFailure()
    {
        // Arrange
        var title = new string('a', 101);

        // Act
        var result = TrainingTitle.Create(title);

        // Assert
        result.ShouldContainError(TrainingErrorCodes.InvalidTitle);
    }

    [Fact]
    public void Equality_SameValueDifferentCase_AreEqual()
    {
        // Arrange
        var title1 = TrainingTitle.Create("Valid Title").ShouldBeSuccess();
        var title2 = TrainingTitle.Create("valid title").ShouldBeSuccess();

        // Assert
        title1.Should().Be(title2);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var title1 = TrainingTitle.Create("First Title").ShouldBeSuccess();
        var title2 = TrainingTitle.Create("Second Title").ShouldBeSuccess();

        // Assert
        title1.Should().NotBe(title2);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var title = TrainingTitle.Create("Valid Title").ShouldBeSuccess();

        // Assert
        title.ToString().Should().Be("Valid Title");
    }
}
