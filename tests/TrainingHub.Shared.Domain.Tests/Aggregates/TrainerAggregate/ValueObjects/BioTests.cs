using AwesomeAssertions;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Tests.Helpers;
using Xunit;

namespace TrainingHub.Shared.Domain.Tests.Aggregates.TrainerAggregate.ValueObjects;

/// <summary>
/// Behavior covered for <c>Bio</c>.
/// </summary>
public sealed class BioTests
{
    /// <summary>
    /// Create, valid bio, returns success.
    /// </summary>
    [Fact]
    public void Create_ValidBio_ReturnsSuccess()
    {
        // Act
        var result = Bio.Create("Experienced software trainer with 10 years of experience.");

        // Assert
        result.ShouldBeSuccess();
    }

    /// <summary>
    /// Create, valid bio, sets value.
    /// </summary>
    [Fact]
    public void Create_ValidBio_SetsValue()
    {
        // Act
        var bio = Bio.Create("Experienced software trainer.").ShouldBeSuccess();

        // Assert
        bio.Value.Should().Be("Experienced software trainer.");
    }

    /// <summary>
    /// Create, null bio, returns failure.
    /// </summary>
    [Fact]
    public void Create_NullBio_ReturnsFailure()
    {
        // Act
        var result = Bio.Create(null!);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.BioEmpty);
    }

    /// <summary>
    /// Create, empty bio, returns failure.
    /// </summary>
    [Fact]
    public void Create_EmptyBio_ReturnsFailure()
    {
        // Act
        var result = Bio.Create(string.Empty);

        // Assert
        result.ShouldContainError(TrainerErrorCodes.BioEmpty);
    }

    /// <summary>
    /// Create, whitespace only bio, returns failure.
    /// </summary>
    [Fact]
    public void Create_WhitespaceOnlyBio_ReturnsFailure()
    {
        // Act
        var result = Bio.Create("   ");

        // Assert
        result.ShouldContainError(TrainerErrorCodes.BioEmpty);
    }

    /// <summary>
    /// Create, exactly max length, returns success.
    /// </summary>
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

    /// <summary>
    /// Create, exceeds max length, returns failure.
    /// </summary>
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

    /// <summary>
    /// Equality, same value, are equal.
    /// </summary>
    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        // Arrange
        var bio1 = Bio.Create("Same bio content.").ShouldBeSuccess();
        var bio2 = Bio.Create("Same bio content.").ShouldBeSuccess();

        // Assert
        bio1.Should().Be(bio2);
    }

    /// <summary>
    /// Equality, different value, are not equal.
    /// </summary>
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
