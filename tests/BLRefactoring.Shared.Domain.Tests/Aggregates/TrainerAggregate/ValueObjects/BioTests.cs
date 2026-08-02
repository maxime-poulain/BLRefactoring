using AwesomeAssertions;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Tests.Helpers;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Aggregates.TrainerAggregate.ValueObjects;

public sealed class BioTests
{
    [Fact]
    public void Create_ValidBio_ReturnsSuccess()
    {
        // Act
        var result = Bio.Create("Experienced software trainer with 10 years of experience.");

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Create_ValidBio_SetsValue()
    {
        // Act
        var bio = Bio.Create("Experienced software trainer.").ShouldBeSuccess();

        // Assert
        bio.Value.Should().Be("Experienced software trainer.");
    }

    [Fact]
    public void Create_NullBio_ReturnsFailure()
    {
        // Act
        var result = Bio.Create(null!);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.BioEmpty);
    }

    [Fact]
    public void Create_EmptyBio_ReturnsFailure()
    {
        // Act
        var result = Bio.Create(string.Empty);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.BioEmpty);
    }

    [Fact]
    public void Create_WhitespaceOnlyBio_ReturnsFailure()
    {
        // Act
        var result = Bio.Create("   ");

        // Assert
        result.ShouldContainError(TrainerErrorCodes.BioEmpty);
    }

    [Fact]
    public void Create_ExactlyMaxLength_ReturnsSuccess()
    {
        // Arrange
        var bio = new string('a', 500);

        // Act
        var result = Bio.Create(bio);

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Create_ExceedsMaxLength_ReturnsFailure()
    {
        // Arrange
        var bio = new string('a', 501);

        // Act
        var result = Bio.Create(bio);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.BioExceeds500Characters);
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        // Arrange
        var bio1 = Bio.Create("Same bio content.").ShouldBeSuccess();
        var bio2 = Bio.Create("Same bio content.").ShouldBeSuccess();

        // Assert
        bio1.Should().Be(bio2);
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        // Arrange
        var bio1 = Bio.Create("First bio content.").ShouldBeSuccess();
        var bio2 = Bio.Create("Second bio content.").ShouldBeSuccess();

        // Assert
        bio1.Should().NotBe(bio2);
    }
}
