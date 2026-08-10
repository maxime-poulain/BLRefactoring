using AwesomeAssertions;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Xunit;

namespace TrainingHub.Shared.Domain.Tests.Aggregates.TrainerAggregate.ValueObjects;

/// <summary>
/// Behavior covered for <c>Name</c>.
/// </summary>
public sealed class NameTests
{
    /// <summary>
    /// Create, valid names, returns success.
    /// </summary>
    [Fact]
    public void Create_ValidNames_ReturnsSuccess()
    {
        // Act
        var result = Name.Create("John", "Doe");

        // Assert
        result.ShouldBeSuccess();
    }

    /// <summary>
    /// Create, valid names, sets firstname and lastname.
    /// </summary>
    [Fact]
    public void Create_ValidNames_SetsFirstnameAndLastname()
    {
        // Act
        var name = Name.Create("John", "Doe").ShouldBeSuccess();

        // Assert
        name.Firstname.Should().Be("John");
        name.Lastname.Should().Be("Doe");
    }

    /// <summary>
    /// Create, firstname too short, returns failure.
    /// </summary>
    [Fact]
    public void Create_FirstnameTooShort_ReturnsFailure()
    {
        // Act
        var result = Name.Create("J", "Doe");

        // Assert
        result.ShouldContainError(TrainerErrorCodes.InvalidFirstname);
    }

    /// <summary>
    /// Create, lastname too short, returns failure.
    /// </summary>
    [Fact]
    public void Create_LastnameTooShort_ReturnsFailure()
    {
        // Act
        var result = Name.Create("John", "D");

        // Assert
        result.ShouldContainError(TrainerErrorCodes.InvalidLastname);
    }

    /// <summary>
    /// Create, both names too short, returns failure with two errors.
    /// </summary>
    [Fact]
    public void Create_BothNamesTooShort_ReturnsFailureWithTwoErrors()
    {
        // Act
        var result = Name.Create("J", "D");

        // Assert
        var errors = result.ShouldBeFailure();
        errors.Should().HaveCount(2);
    }

    /// <summary>
    /// Create, firstname exactly min length, returns success.
    /// </summary>
    [Fact]
    public void Create_FirstnameExactlyMinLength_ReturnsSuccess()
    {
        // Act
        var result = Name.Create("Jo", "Doe");

        // Assert
        result.ShouldBeSuccess();
    }

    /// <summary>
    /// Create, firstname exactly max length, returns success.
    /// </summary>
    [Fact]
    public void Create_FirstnameExactlyMaxLength_ReturnsSuccess()
    {
        // Arrange
        var firstname = new string('a', 50);

        // Act
        var result = Name.Create(firstname, "Doe");

        // Assert
        result.ShouldBeSuccess();
    }

    /// <summary>
    /// Create, firstname too long, returns failure.
    /// </summary>
    [Fact]
    public void Create_FirstnameTooLong_ReturnsFailure()
    {
        // Arrange
        var firstname = new string('a', 51);

        // Act
        var result = Name.Create(firstname, "Doe");

        // Assert
        result.ShouldContainError(TrainerErrorCodes.InvalidFirstname);
    }

    /// <summary>
    /// Create, null firstname, returns failure.
    /// </summary>
    [Fact]
    public void Create_NullFirstname_ReturnsFailure()
    {
        // Act
        var result = Name.Create(null!, "Doe");

        // Assert
        result.ShouldBeFailure();
    }

    /// <summary>
    /// Create, surrounding whitespace, stores the trimmed name.
    /// </summary>
    /// <remarks>
    /// Whitespace is how a value was typed, not what it is: without trimming, <c>" Bob "</c> and
    /// <c>"Bob"</c> are two different trainers to every equality check that reads them. See
    /// ADR 0044.
    /// </remarks>
    [Fact]
    public void Create_SurroundingWhitespace_StoresTheTrimmedName()
    {
        // Act
        var name = Name.Create("  Bob  ", "  Smith  ").ShouldBeSuccess();

        // Assert
        name.Firstname.Should().Be("Bob");
        name.Lastname.Should().Be("Smith");
    }

    /// <summary>
    /// Create, only whitespace, returns failure.
    /// </summary>
    /// <remarks>
    /// The length is measured on what would be stored, so five spaces is not a two-character name.
    /// This passed before trimming, for the wrong reason.
    /// </remarks>
    [Fact]
    public void Create_OnlyWhitespace_ReturnsFailure()
    {
        // Act
        var result = Name.Create("     ", "Smith");

        // Assert
        result.ShouldContainError(TrainerErrorCodes.InvalidFirstname);
    }

    /// <summary>
    /// Equality, same names, are equal.
    /// </summary>
    [Fact]
    public void Equality_SameNames_AreEqual()
    {
        // Arrange
        var name1 = Name.Create("John", "Doe").ShouldBeSuccess();
        var name2 = Name.Create("John", "Doe").ShouldBeSuccess();

        // Assert
        name1.Should().Be(name2);
    }

    /// <summary>
    /// Equality, different names, are not equal.
    /// </summary>
    [Fact]
    public void Equality_DifferentNames_AreNotEqual()
    {
        // Arrange
        var name1 = Name.Create("John", "Doe").ShouldBeSuccess();
        var name2 = Name.Create("Jane", "Smith").ShouldBeSuccess();

        // Assert
        name1.Should().NotBe(name2);
    }
}
