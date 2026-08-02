using AwesomeAssertions;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Aggregates.TrainerAggregate.ValueObjects;

public sealed class EmailTests
{
    [Fact]
    public void Create_ValidEmail_ReturnsSuccess()
    {
        // Act
        var result = Email.Create("john.doe@example.com");

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Create_ValidEmail_SetsFullAddress()
    {
        // Act
        var email = Email.Create("john.doe@example.com").ShouldBeSuccess();

        // Assert
        email.FullAddress.Should().Be("john.doe@example.com");
    }

    [Fact]
    public void Create_ValidEmail_SetsLocalPartAndDomain()
    {
        // Act
        var email = Email.Create("john.doe@example.com").ShouldBeSuccess();

        // Assert
        email.LocalPart.Should().Be("john.doe");
        email.Domain.Should().Be("example.com");
    }

    [Fact]
    public void LocalPartAndDomain_SplitOnTheLastSeparator()
    {
        // A quoted local part may itself contain an `@`; a domain never contains an unquoted one.
        // So the separator is the last `@`, not the first — splitting on the first reported a
        // domain of `b"` for this address.
        var email = Email.Create("\"a@b\"@example.com").ShouldBeSuccess();

        email.LocalPart.Should().Be("\"a@b\"");
        email.Domain.Should().Be("example.com");
    }

    [Fact]
    public void Create_NullEmail_ReturnsFailure()
    {
        // Act
        var result = Email.Create(null!);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.InvalidEmail);
    }

    [Fact]
    public void Create_EmptyString_ReturnsFailure()
    {
        // Act
        var result = Email.Create(string.Empty);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.InvalidEmail);
    }

    [Fact]
    public void Create_WhitespaceOnly_ReturnsFailure()
    {
        // Act
        var result = Email.Create("   ");

        // Assert
        result.ShouldContainError(TrainerErrorCodes.InvalidEmail);
    }

    [Fact]
    public void Create_InvalidFormat_ReturnsFailure()
    {
        // Act
        var result = Email.Create("notanemail");

        // Assert
        result.ShouldContainError(TrainerErrorCodes.InvalidEmail);
    }

    [Fact]
    public void Equality_SameAddress_AreEqual()
    {
        // Arrange
        var email1 = Email.Create("john.doe@example.com").ShouldBeSuccess();
        var email2 = Email.Create("john.doe@example.com").ShouldBeSuccess();

        // Assert
        email1.Should().Be(email2);
    }

    [Fact]
    public void Equality_DifferentAddress_AreNotEqual()
    {
        // Arrange
        var email1 = Email.Create("john.doe@example.com").ShouldBeSuccess();
        var email2 = Email.Create("jane.doe@example.com").ShouldBeSuccess();

        // Assert
        email1.Should().NotBe(email2);
    }
}
