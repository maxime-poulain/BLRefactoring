using AwesomeAssertions;
using BLRefactoring.Shared.Common;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Common;

public class ValueObjectTests
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

    [Fact]
    public void Equals_SameComponents_ReturnsTrue()
    {
        var vo1 = new TestValueObject("A", "B");
        var vo2 = new TestValueObject("A", "B");

        vo1.Equals(vo2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentComponents_ReturnsFalse()
    {
        var vo1 = new TestValueObject("A", "B");
        var vo2 = new TestValueObject("A", "C");

        vo1.Equals(vo2).Should().BeFalse();
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var vo = new TestValueObject("A", "B");

        vo.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_DifferentType_ReturnsFalse()
    {
        var vo = new TestValueObject("A", "B");
        object other = "not a value object";

        vo.Equals(other).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_SameComponents_ReturnsSameHash()
    {
        var vo1 = new TestValueObject("A", "B");
        var vo2 = new TestValueObject("A", "B");

        vo1.GetHashCode().Should().Be(vo2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentComponents_ReturnsDifferentHash()
    {
        var vo1 = new TestValueObject("A", "B");
        var vo2 = new TestValueObject("A", "C");

        vo1.GetHashCode().Should().NotBe(vo2.GetHashCode());
    }

    [Fact]
    public void OperatorEquals_SameComponents_ReturnsTrue()
    {
        var vo1 = new TestValueObject("A", "B");
        var vo2 = new TestValueObject("A", "B");

        (vo1 == vo2).Should().BeTrue();
    }

    [Fact]
    public void OperatorNotEquals_DifferentComponents_ReturnsTrue()
    {
        var vo1 = new TestValueObject("A", "B");
        var vo2 = new TestValueObject("A", "C");

        (vo1 != vo2).Should().BeTrue();
    }

}
