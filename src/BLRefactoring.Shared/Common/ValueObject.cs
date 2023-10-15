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
public abstract class ValueObject : IComparable, IComparable<ValueObject>
{
    private int? _cachedHashCode;

    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj == null)
        {
            return false;
        }

        if (GetType() == obj.GetType())
        {
            var valueObject = (ValueObject)obj;
            return GetEqualityComponents().SequenceEqual(valueObject.GetEqualityComponents());
        }

        return false;
    }

    public override int GetHashCode()
    {
        _cachedHashCode ??= GetEqualityComponents()
            .Aggregate(1, (current, obj) =>
            {
                unchecked
                {
                    return current * 23 + (obj?.GetHashCode() ?? 0);
                }
            });

        return _cachedHashCode.Value;
    }

    public virtual int CompareTo(object? obj)
    {
        if (obj is null)
        {
            return 1;
        }

        if (ReferenceEquals(this, obj))
        {
            return 0;
        }

        var thisType = GetType();
        var otherType = obj.GetType();

        if (thisType != otherType)
        {
            return string.Compare(thisType.ToString(), otherType.ToString(), StringComparison.Ordinal);
        }

        var other = (ValueObject)obj;

        var components = GetEqualityComponents().ToArray();
        var otherComponents = other.GetEqualityComponents().ToArray();

        for (var i = 0; i < components.Length; i++)
        {
            var comparison = CompareComponents(components[i], otherComponents[i]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    public virtual int CompareTo(ValueObject? other) => CompareTo(other as object);

    private static int CompareComponents(object? object1, object? object2)
    {
        if (object1 is null && object2 is null)
        {
            return 0;
        }

        if (object1 is null)
        {
            return -1;
        }

        if (object2 is null)
        {
            return 1;
        }

        if (object1 is IComparable comparable1 && object2 is IComparable comparable2)
        {
            return comparable1.CompareTo(comparable2);
        }

        return object1.Equals(object2) ? 0 : -1;
    }

    public static bool operator ==(ValueObject? a, ValueObject? b)
    {
        if (a is null && b is null)
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        return a.Equals(b);
    }

    public static bool operator !=(ValueObject? a, ValueObject? b)
    {
        return !(a == b);
    }
}
