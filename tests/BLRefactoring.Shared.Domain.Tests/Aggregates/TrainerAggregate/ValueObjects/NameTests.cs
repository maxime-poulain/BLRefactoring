using AwesomeAssertions;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Aggregates.TrainerAggregate.ValueObjects;

public sealed class NameTests
{
    [Fact]
    public void Create_ValidNames_ReturnsSuccess()
    {
        // Act
        var result = Name.Create("John", "Doe");

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Create_ValidNames_SetsFirstnameAndLastname()
    {
        // Act
        var name = Name.Create("John", "Doe").ShouldBeSuccess();

        // Assert
        name.Firstname.Should().Be("John");
        name.Lastname.Should().Be("Doe");
    }

    [Fact]
    public void Create_FirstnameTooShort_ReturnsFailure()
    {
        // Act
        var result = Name.Create("J", "Doe");

        // Assert
        result.ShouldContainError(ErrorCodes.Unspecified);
    }

    [Fact]
    public void Create_LastnameTooShort_ReturnsFailure()
    {
        // Act
        var result = Name.Create("John", "D");

        // Assert
        result.ShouldContainError(ErrorCodes.Unspecified);
    }

    [Fact]
    public void Create_BothNamesTooShort_ReturnsFailureWithTwoErrors()
    {
        // Act
        var result = Name.Create("J", "D");

        // Assert
        var errors = result.ShouldBeFailure();
        errors.Should().HaveCount(2);
    }

    [Fact]
    public void Create_FirstnameExactlyMinLength_ReturnsSuccess()
    {
        // Act
        var result = Name.Create("Jo", "Doe");

        // Assert
        result.ShouldBeSuccess();
    }

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

    [Fact]
    public void Create_FirstnameTooLong_ReturnsFailure()
    {
        // Arrange
        var firstname = new string('a', 51);

        // Act
        var result = Name.Create(firstname, "Doe");

        // Assert
        result.ShouldContainError(ErrorCodes.Unspecified);
    }

    [Fact]
    public void Create_NullFirstname_ReturnsFailure()
    {
        // Act
        var result = Name.Create(null!, "Doe");

        // Assert
        result.ShouldBeFailure();
    }

    [Fact]
    public void Equality_SameNames_AreEqual()
    {
        // Arrange
        var name1 = Name.Create("John", "Doe").ShouldBeSuccess();
        var name2 = Name.Create("John", "Doe").ShouldBeSuccess();

        // Assert
        name1.Should().Be(name2);
    }

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
