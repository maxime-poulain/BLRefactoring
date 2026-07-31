using AwesomeAssertions;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Aggregates.TrainingAggregate;

public class TrainingIdTests
{
    [Fact]
    public void Generate_ReturnsNonEmpty()
    {
        // Act
        var trainingId = TrainingId.Generate();

        // Assert
        trainingId.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_WithGuid_SetsValue()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var trainingId = TrainingId.Create(guid);

        // Assert
        trainingId.Value.Should().Be(guid);
    }

    [Fact]
    public void Value_ExposesTheUnderlyingGuid()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var trainingId = TrainingId.Create(guid);

        // Assert
        trainingId.Value.Should().Be(guid);
    }

    [Fact]
    public void ExplicitConversion_FromGuid_CreatesValidatedId()
    {
        // The idiom this codebase writes at its boundaries: `TrainingId id = (TrainingId)someGuid`.
        // The cast is deliberate — turning a loose Guid into an identifier can fail, and an
        // implicit conversion would hide both the intent and the failure.
        var guid = Guid.NewGuid();

        var trainingId = (TrainingId)guid;

        trainingId.Value.Should().Be(guid);
    }

    [Fact]
    public void ExplicitConversion_FromEmptyGuid_Throws()
    {
        // Same validation as Create: the conversion is a different spelling of it, not a
        // shortcut around it.
        var act = () => (TrainingId)Guid.Empty;

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_EmptyGuid_Throws()
    {
        // Act
        var act = () => TrainingId.Create(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_SameGuid_AreEqual()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var trainingId1 = TrainingId.Create(guid);
        var trainingId2 = TrainingId.Create(guid);

        // Assert
        trainingId1.Should().Be(trainingId2);
        (trainingId1 == trainingId2).Should().BeTrue();
    }
}
