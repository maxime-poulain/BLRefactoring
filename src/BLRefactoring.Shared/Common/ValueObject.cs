namespace BLRefactoring.Shared.Common;

/// <summary>
/// A Value Object (VO) is a Domain Object that represents a concept based on
/// its attributes and carries no concept of identity.
/// It should be immutable and its equality is defined by the equality
/// of its attributes.
/// VOs are usually used as a property of an Entity or another VO, and can be shared
/// between different Entities.
/// They model object attributes or characteristics that are transient
/// and do not have identity.
/// </summary>
/// <remarks>
/// <para>
/// According to Eric Evans in "Domain-Driven Design", "A VALUE OBJECT is an object
/// that describes some characteristic or attribute but carries no concept of identity.
/// Value objects are distinguished by the fact that two value objects with
/// the same attributes are interchangeable, no matter where or when they were created."
/// </para>
/// <para>
/// Vaughn Vernon in "Implementing Domain-Driven Design" explains that "Value Objects
/// are an important DDD concept. They model object attributes or characteristics that
/// are transient and do not have identity. They are usually small and immutable, and can be used for comparisons."
/// </para>
/// <para>
/// Scott Millett and Nick Tune in "Patterns, Principles, and Practices of Domain-Driven Design" state that
/// "Value Objects represent some concept or idea, where the concept is defined by the state of its properties.
/// Two Value Objects are considered equal if they have the same properties. They are typically immutable, so they cannot be changed once created."
/// </para>
/// </remarks>
[Serializable]
public abstract class ValueObject : IEquatable<ValueObject>, IComparable, IComparable<ValueObject>
{
    private int? _cachedHashCode;

    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;

        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public sealed override bool Equals(object? obj)
    {
        return obj is ValueObject other && Equals(other);
    }

    public sealed override int GetHashCode()
    {
        if (_cachedHashCode.HasValue)
            return _cachedHashCode.Value;

        var hash = new HashCode();
        foreach (var component in GetEqualityComponents())
        {
            hash.Add(component);
        }

        _cachedHashCode = hash.ToHashCode();
        return _cachedHashCode.Value;
    }

    public virtual int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (ReferenceEquals(this, obj)) return 0;

        if (obj is not ValueObject other)
            throw new ArgumentException(
                $"Object must be of type {nameof(ValueObject)}");

        if (GetType() != other.GetType())
            return string.Compare(GetType().ToString(),
                other.GetType().ToString(), StringComparison.Ordinal);

        return CompareComponents(other);
    }

    public virtual int CompareTo(ValueObject? other)
    {
        if (other is null) return 1;
        if (ReferenceEquals(this, other)) return 0;

        return CompareComponents(other);
    }

    private int CompareComponents(ValueObject other)
    {
        var components = GetEqualityComponents().ToArray();
        var otherComponents = other.GetEqualityComponents().ToArray();

        for (var i = 0; i < components.Length; i++)
        {
            var comparison = CompareValues(components[i], otherComponents[i]);
            if (comparison != 0)
                return comparison;
        }

        return 0;
    }

    private static int CompareValues(object? left, object? right)
    {
        if (left is null && right is null) return 0;
        if (left is null) return -1;
        if (right is null) return 1;

        if (left is IComparable comparable)
            return comparable.CompareTo(right);

        return left.Equals(right) ? 0 : -1;
    }

    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return !Equals(left, right);
    }
}
