using AwesomeAssertions;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Aggregates.TrainerAggregate;

public class TrainerIdTests
{
    [Fact]
    public void Generate_ReturnsNonEmpty()
    {
        // Act
        var trainerId = TrainerId.Generate();

        // Assert
        trainerId.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_WithGuid_SetsValue()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var trainerId = TrainerId.Create(guid);

        // Assert
        trainerId.Value.Should().Be(guid);
    }

    [Fact]
    public void Value_ExposesTheUnderlyingGuid()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var trainerId = TrainerId.Create(guid);

        // Assert
        trainerId.Value.Should().Be(guid);
    }

    [Fact]
    public void ExplicitConversion_FromGuid_CreatesValidatedId()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var trainerId = (TrainerId)guid;

        // Assert
        trainerId.Value.Should().Be(guid);
    }

    [Fact]
    public void ExplicitConversion_FromEmptyGuid_Throws()
    {
        // Act
        var act = () => (TrainerId)Guid.Empty;

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_EmptyGuid_Throws()
    {
        // Act
        var act = () => TrainerId.Create(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_SameGuid_AreEqual()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var trainerId1 = TrainerId.Create(guid);
        var trainerId2 = TrainerId.Create(guid);

        // Assert
        trainerId1.Should().Be(trainerId2);
        (trainerId1 == trainerId2).Should().BeTrue();
    }
}
