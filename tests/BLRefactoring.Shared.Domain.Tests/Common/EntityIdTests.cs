using AwesomeAssertions;
using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Common;

public class EntityIdTests
{
    [Fact]
    public void Generate_ReturnsNonEmptyGuid()
    {
        var id = TrainerId.Generate();

        id.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void Generate_ReturnsDifferentIdsEachTime()
    {
        var id1 = TrainerId.Generate();
        var id2 = TrainerId.Generate();

        id1.Value.Should().NotBe(id2.Value);
    }

    [Fact]
    public void Create_WithGuid_SetsValue()
    {
        var guid = Guid.NewGuid();

        var id = TrainerId.Create(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void Equals_SameValue_ReturnsTrue()
    {
        var guid = Guid.NewGuid();
        var id1 = TrainerId.Create(guid);
        var id2 = TrainerId.Create(guid);

        id1.Equals(id2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        var id1 = TrainerId.Create(Guid.NewGuid());
        var id2 = TrainerId.Create(Guid.NewGuid());

        id1.Equals(id2).Should().BeFalse();
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var id = TrainerId.Create(Guid.NewGuid());

        id.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_SameValue_ReturnsSameHash()
    {
        var guid = Guid.NewGuid();
        var id1 = TrainerId.Create(guid);
        var id2 = TrainerId.Create(guid);

        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Fact]
    public void CompareTo_Null_ReturnsPositive()
    {
        var id = TrainerId.Create(Guid.NewGuid());

        id.CompareTo((EntityId<TrainerId, Guid>?)null).Should().Be(1);
    }

    [Fact]
    public void CompareTo_SameValue_ReturnsZero()
    {
        var guid = Guid.NewGuid();
        var id1 = TrainerId.Create(guid);
        var id2 = TrainerId.Create(guid);

        id1.CompareTo(id2).Should().Be(0);
    }

    [Fact]
    public void Value_ExposesTheUnderlyingGuid()
    {
        var guid = Guid.NewGuid();
        var id = TrainerId.Create(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void Create_DefaultValue_Throws()
    {
        var act = () => TrainerId.Create(Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Generate_ProducesNonEmptyValue()
    {
        var id = TrainerId.Generate();

        id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void OperatorEquals_SameValue_ReturnsTrue()
    {
        var guid = Guid.NewGuid();
        var id1 = TrainerId.Create(guid);
        var id2 = TrainerId.Create(guid);

        (id1 == id2).Should().BeTrue();
    }

    [Fact]
    public void OperatorNotEquals_DifferentValue_ReturnsTrue()
    {
        var id1 = TrainerId.Create(Guid.NewGuid());
        var id2 = TrainerId.Create(Guid.NewGuid());

        (id1 != id2).Should().BeTrue();
    }
}
