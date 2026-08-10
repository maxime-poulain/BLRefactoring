using AwesomeAssertions;
using TrainingHub.Shared.Common;
using Xunit;

namespace TrainingHub.Shared.Domain.Tests.Common;

/// <summary>
/// Behavior covered for <c>ValueObject</c>.
/// </summary>
public sealed class ValueObjectTests
{
    private sealed class TestValueObject : ValueObject
    {
        public string Component1 { get; }
        public string Component2 { get; }

        public TestValueObject(string component1, string component2)
        {
            Component1 = component1;
            Component2 = component2;
        }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Component1;
            yield return Component2;
        }
    }

    /// <summary>
    /// Equals, same components, returns true.
    /// </summary>
    [Fact]
    public void Equals_SameComponents_ReturnsTrue()
    {
        var vo1 = new TestValueObject("A", "B");
        var vo2 = new TestValueObject("A", "B");

        vo1.Equals(vo2).Should().BeTrue();
    }

    /// <summary>
    /// Equals, different components, returns false.
    /// </summary>
    [Fact]
    public void Equals_DifferentComponents_ReturnsFalse()
    {
        var vo1 = new TestValueObject("A", "B");
        var vo2 = new TestValueObject("A", "C");

        vo1.Equals(vo2).Should().BeFalse();
    }

    /// <summary>
    /// Equals, null, returns false.
    /// </summary>
    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var vo = new TestValueObject("A", "B");

        vo.Equals(null).Should().BeFalse();
    }

    /// <summary>
    /// Equals, different type, returns false.
    /// </summary>
    [Fact]
    public void Equals_DifferentType_ReturnsFalse()
    {
        var vo = new TestValueObject("A", "B");
        object other = "not a value object";

        vo.Equals(other).Should().BeFalse();
    }

    /// <summary>
    /// Get hash code, same components, returns same hash.
    /// </summary>
    [Fact]
    public void GetHashCode_SameComponents_ReturnsSameHash()
    {
        var vo1 = new TestValueObject("A", "B");
        var vo2 = new TestValueObject("A", "B");

        vo1.GetHashCode().Should().Be(vo2.GetHashCode());
    }

    /// <summary>
    /// Get hash code, different components, returns different hash.
    /// </summary>
    [Fact]
    public void GetHashCode_DifferentComponents_ReturnsDifferentHash()
    {
        var vo1 = new TestValueObject("A", "B");
        var vo2 = new TestValueObject("A", "C");

        vo1.GetHashCode().Should().NotBe(vo2.GetHashCode());
    }

    /// <summary>
    /// Operator equals, same components, returns true.
    /// </summary>
    [Fact]
    public void OperatorEquals_SameComponents_ReturnsTrue()
    {
        var vo1 = new TestValueObject("A", "B");
        var vo2 = new TestValueObject("A", "B");

        (vo1 == vo2).Should().BeTrue();
    }

    /// <summary>
    /// Operator not equals, different components, returns true.
    /// </summary>
    [Fact]
    public void OperatorNotEquals_DifferentComponents_ReturnsTrue()
    {
        var vo1 = new TestValueObject("A", "B");
        var vo2 = new TestValueObject("A", "C");

        (vo1 != vo2).Should().BeTrue();
    }

}
