using System.Diagnostics.CodeAnalysis;
using AwesomeAssertions;
using TrainingHub.Shared.Common;
using Xunit;

namespace TrainingHub.Shared.Domain.Tests.Common;

/// <summary>
/// Behavior covered for <c>Entity</c>.
/// </summary>
public sealed class EntityTests
{
    /// <summary>
    /// Test entity id.
    /// </summary>
    public sealed class TestEntityId : EntityId<TestEntityId>
    {
        [SuppressMessage("Style", "IDE0051:Remove unused private members",
            Justification = "EntityId<T>.BuildFactory resolves this constructor with GetConstructor(..., NonPublic) and compiles it into the factory every Create and Generate call goes through. It is the only way an identifier is ever built; the analyzer cannot see a call that a compiled expression tree makes.")]
        private TestEntityId(Guid value) : base(value) { }
    }

    /// <summary>
    /// Test entity.
    /// </summary>
    public sealed class TestEntity(TestEntityId id) : Entity<TestEntityId>(id);

    /// <summary>
    /// New entity, exposes the provided id.
    /// </summary>
    [Fact]
    public void NewEntity_ExposesTheProvidedId()
    {
        var id = TestEntityId.Generate();

        var entity = new TestEntity(id);

        entity.Id.Should().Be(id);
    }

    /// <summary>
    /// New entity, null id, throws.
    /// </summary>
    [Fact]
    public void NewEntity_NullId_Throws()
    {
        var act = () => new TestEntity(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Equals, same id, returns true.
    /// </summary>
    [Fact]
    public void Equals_SameId_ReturnsTrue()
    {
        var id = TestEntityId.Generate();
        var entity1 = new TestEntity(id);
        var entity2 = new TestEntity(id);

        entity1.Equals(entity2).Should().BeTrue();
    }

    /// <summary>
    /// Equals, different id, returns false.
    /// </summary>
    [Fact]
    public void Equals_DifferentId_ReturnsFalse()
    {
        var entity1 = new TestEntity(TestEntityId.Generate());
        var entity2 = new TestEntity(TestEntityId.Generate());

        entity1.Equals(entity2).Should().BeFalse();
    }

    /// <summary>
    /// Equals, null, returns false.
    /// </summary>
    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var entity = new TestEntity(TestEntityId.Generate());

        entity.Equals(null).Should().BeFalse();
    }

    /// <summary>
    /// Equals, different type, returns false.
    /// </summary>
    [Fact]
    public void Equals_DifferentType_ReturnsFalse()
    {
        var entity = new TestEntity(TestEntityId.Generate());
        object other = "not an entity";

        entity.Equals(other).Should().BeFalse();
    }

    /// <summary>
    /// Get hash code, same id, returns same hash code.
    /// </summary>
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

    /// <summary>
    /// Get hash code, different id, returns different hash code.
    /// </summary>
    [Fact]
    public void GetHashCode_DifferentId_ReturnsDifferentHashCode()
    {
        var entity1 = new TestEntity(TestEntityId.Generate());
        var entity2 = new TestEntity(TestEntityId.Generate());

        entity1.GetHashCode().Should().NotBe(entity2.GetHashCode());
    }

    /// <summary>
    /// Operator equals, same id, returns true.
    /// </summary>
    [Fact]
    public void OperatorEquals_SameId_ReturnsTrue()
    {
        var entity = new TestEntity(TestEntityId.Generate());
        var sameRef = entity;

        (entity == sameRef).Should().BeTrue();
    }

    /// <summary>
    /// Operator equals, both null, returns true.
    /// </summary>
    [Fact]
    public void OperatorEquals_BothNull_ReturnsTrue()
    {
        TestEntity? a = null;
        TestEntity? b = null;

        (a == b).Should().BeTrue();
    }

    /// <summary>
    /// Operator equals, one null, returns false.
    /// </summary>
    [Fact]
    public void OperatorEquals_OneNull_ReturnsFalse()
    {
        var entity = new TestEntity(TestEntityId.Generate());
        TestEntity? nullEntity = null;

        (entity == nullEntity).Should().BeFalse();
    }

    /// <summary>
    /// Operator not equals, different id, returns true.
    /// </summary>
    [Fact]
    public void OperatorNotEquals_DifferentId_ReturnsTrue()
    {
        var entity1 = new TestEntity(TestEntityId.Generate());
        var entity2 = new TestEntity(TestEntityId.Generate());

        (entity1 != entity2).Should().BeTrue();
    }
}
