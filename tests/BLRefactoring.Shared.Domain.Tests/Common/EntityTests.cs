using BLRefactoring.Shared.Common;
using FluentAssertions;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Common;

public class EntityTests
{
    public class TestEntityId : EntityId<TestEntityId> { }

    public class TestEntity(TestEntityId id) : Entity<TestEntityId>(id);

    [Fact]
    public void NewEntity_ExposesTheProvidedId()
    {
        var id = TestEntityId.Generate();

        var entity = new TestEntity(id);

        entity.Id.Should().Be(id);
    }

    [Fact]
    public void NewEntity_NullId_Throws()
    {
        var act = () => new TestEntity(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Equals_SameId_ReturnsTrue()
    {
        var id = TestEntityId.Generate();
        var entity1 = new TestEntity(id);
        var entity2 = new TestEntity(id);

        entity1.Equals(entity2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentId_ReturnsFalse()
    {
        var entity1 = new TestEntity(TestEntityId.Generate());
        var entity2 = new TestEntity(TestEntityId.Generate());

        entity1.Equals(entity2).Should().BeFalse();
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var entity = new TestEntity(TestEntityId.Generate());

        entity.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_DifferentType_ReturnsFalse()
    {
        var entity = new TestEntity(TestEntityId.Generate());
        object other = "not an entity";

        entity.Equals(other).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_SameId_ReturnsSameHashCode()
    {
        var id = TestEntityId.Generate();
        var entity1 = new TestEntity(id);
        var entity2 = new TestEntity(id);

        // Equal entities (same Id) must have equal hash codes,
        // as required by the Equals/GetHashCode contract.
        entity1.GetHashCode().Should().Be(entity2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentId_ReturnsDifferentHashCode()
    {
        var entity1 = new TestEntity(TestEntityId.Generate());
        var entity2 = new TestEntity(TestEntityId.Generate());

        entity1.GetHashCode().Should().NotBe(entity2.GetHashCode());
    }

    [Fact]
    public void OperatorEquals_SameId_ReturnsTrue()
    {
        var entity = new TestEntity(TestEntityId.Generate());
        var sameRef = entity;

        (entity == sameRef).Should().BeTrue();
    }

    [Fact]
    public void OperatorEquals_BothNull_ReturnsTrue()
    {
        TestEntity? a = null;
        TestEntity? b = null;

        (a == b).Should().BeTrue();
    }

    [Fact]
    public void OperatorEquals_OneNull_ReturnsFalse()
    {
        var entity = new TestEntity(TestEntityId.Generate());
        TestEntity? nullEntity = null;

        (entity == nullEntity).Should().BeFalse();
    }

    [Fact]
    public void OperatorNotEquals_DifferentId_ReturnsTrue()
    {
        var entity1 = new TestEntity(TestEntityId.Generate());
        var entity2 = new TestEntity(TestEntityId.Generate());

        (entity1 != entity2).Should().BeTrue();
    }
}
