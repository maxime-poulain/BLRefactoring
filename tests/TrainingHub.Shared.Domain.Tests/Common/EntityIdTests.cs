using AwesomeAssertions;
using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using Xunit;

namespace TrainingHub.Shared.Domain.Tests.Common;

/// <summary>
/// Behaviour covered for <c>EntityId</c>.
/// </summary>
public sealed class EntityIdTests
{
    /// <summary>
    /// Generate, returns non empty guid.
    /// </summary>
    [Fact]
    public void Generate_ReturnsNonEmptyGuid()
    {
        var id = TrainerId.Generate();

        id.Value.Should().NotBeEmpty();
    }

    /// <summary>
    /// Generate, returns different ids each time.
    /// </summary>
    [Fact]
    public void Generate_ReturnsDifferentIdsEachTime()
    {
        var id1 = TrainerId.Generate();
        var id2 = TrainerId.Generate();

        id1.Value.Should().NotBe(id2.Value);
    }

    /// <summary>
    /// Create, with guid, sets value.
    /// </summary>
    [Fact]
    public void Create_WithGuid_SetsValue()
    {
        var guid = Guid.NewGuid();

        var id = TrainerId.Create(guid);

        id.Value.Should().Be(guid);
    }

    /// <summary>
    /// Equals, same value, returns true.
    /// </summary>
    [Fact]
    public void Equals_SameValue_ReturnsTrue()
    {
        var guid = Guid.NewGuid();
        var id1 = TrainerId.Create(guid);
        var id2 = TrainerId.Create(guid);

        id1.Equals(id2).Should().BeTrue();
    }

    /// <summary>
    /// Equals, different value, returns false.
    /// </summary>
    [Fact]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        var id1 = TrainerId.Create(Guid.NewGuid());
        var id2 = TrainerId.Create(Guid.NewGuid());

        id1.Equals(id2).Should().BeFalse();
    }

    /// <summary>
    /// Equals, null, returns false.
    /// </summary>
    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var id = TrainerId.Create(Guid.NewGuid());

        id.Equals(null).Should().BeFalse();
    }

    /// <summary>
    /// Get hash code, same value, returns same hash.
    /// </summary>
    [Fact]
    public void GetHashCode_SameValue_ReturnsSameHash()
    {
        var guid = Guid.NewGuid();
        var id1 = TrainerId.Create(guid);
        var id2 = TrainerId.Create(guid);

        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    /// <summary>
    /// Compare to, null, returns positive.
    /// </summary>
    [Fact]
    public void CompareTo_Null_ReturnsPositive()
    {
        var id = TrainerId.Create(Guid.NewGuid());

        id.CompareTo(null).Should().Be(1);
    }

    /// <summary>
    /// Compare to, same value, returns zero.
    /// </summary>
    [Fact]
    public void CompareTo_SameValue_ReturnsZero()
    {
        var guid = Guid.NewGuid();
        var id1 = TrainerId.Create(guid);
        var id2 = TrainerId.Create(guid);

        id1.CompareTo(id2).Should().Be(0);
    }

    /// <summary>
    /// Value, exposes the underlying guid.
    /// </summary>
    [Fact]
    public void Value_ExposesTheUnderlyingGuid()
    {
        var guid = Guid.NewGuid();
        var id = TrainerId.Create(guid);

        id.Value.Should().Be(guid);
    }

    /// <summary>
    /// Create, default value, throws.
    /// </summary>
    [Fact]
    public void Create_DefaultValue_Throws()
    {
        var act = () => TrainerId.Create(Guid.Empty);

        // The named type rather than ArgumentException: the base type would also be satisfied by a
        // throw from somewhere else entirely, which is the assertion this one replaces.
        act.Should().Throw<EmptyIdentifierException>()
            .Which.IdentifierType.Should().Be(nameof(TrainerId));
    }

    /// <summary>
    /// Generate, produces non empty value.
    /// </summary>
    [Fact]
    public void Generate_ProducesNonEmptyValue()
    {
        var id = TrainerId.Generate();

        id.Value.Should().NotBe(Guid.Empty);
    }

    /// <summary>
    /// Operator equals, same value, returns true.
    /// </summary>
    [Fact]
    public void OperatorEquals_SameValue_ReturnsTrue()
    {
        var guid = Guid.NewGuid();
        var id1 = TrainerId.Create(guid);
        var id2 = TrainerId.Create(guid);

        (id1 == id2).Should().BeTrue();
    }

    /// <summary>
    /// Operator not equals, different value, returns true.
    /// </summary>
    [Fact]
    public void OperatorNotEquals_DifferentValue_ReturnsTrue()
    {
        var id1 = TrainerId.Create(Guid.NewGuid());
        var id2 = TrainerId.Create(Guid.NewGuid());

        (id1 != id2).Should().BeTrue();
    }
}
