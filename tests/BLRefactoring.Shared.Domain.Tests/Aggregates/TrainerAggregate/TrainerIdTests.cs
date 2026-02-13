using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using FluentAssertions;
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
    public void ImplicitConversion_ToGuid_Works()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var trainerId = TrainerId.Create(guid);

        // Act
        Guid result = trainerId;

        // Assert
        result.Should().Be(guid);
    }

    [Fact]
    public void ExplicitConversion_FromGuid_Works()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var trainerId = (TrainerId)guid;

        // Assert
        trainerId.Value.Should().Be(guid);
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
