namespace TrainingHub.Shared.Common;

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
/// <para>
/// Equality only, deliberately. Value objects used to implement <see cref="IComparable"/> as
/// well, over a component-by-component comparison that fell back to <c>Equals(other) ? 0 : -1</c>
/// for any component that was not itself comparable — which reports <c>a &lt; b</c> and
/// <c>b &lt; a</c> at the same time and breaks any sort built on it. Nothing ever ordered a value
/// object, so the surface went rather than being fixed. One that genuinely needs ordering can
/// implement <see cref="IComparable{T}"/> itself, over components it knows how to compare.
/// </para>
/// </remarks>
[Serializable]
public abstract class ValueObject : IEquatable<ValueObject>
{
    private int? _cachedHashCode;

    /// <summary>
    /// Yields the parts that make up this value's identity, in a fixed order.
    /// </summary>
    /// <returns>
    /// The components equality and hashing are computed from. Two values are the same exactly
    /// when these sequences match, so anything omitted here is deliberately not part of the
    /// value — and anything added later changes what equality means.
    /// </returns>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    /// <summary>
    /// Compares two value objects by their components.
    /// </summary>
    /// <param name="other">The value to compare against, possibly <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> when both have the same runtime type and equal components. A value
    /// object has no identity beyond what it holds, which is what separates it from
    /// <see cref="Entity{TEntityId}"/>.
    /// </returns>
    public bool Equals(ValueObject? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (GetType() != other.GetType())
        {
            return false;
        }

        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    /// <summary>
    /// Compares this value with an arbitrary object.
    /// </summary>
    /// <param name="obj">The object to compare against.</param>
    /// <returns><see langword="true"/> when it is a value object equal to this one.</returns>
    public sealed override bool Equals(object? obj)
    {
        return obj is ValueObject other && Equals(other);
    }

    /// <summary>
    /// Combines the equality components into a hash, computed once and kept.
    /// </summary>
    /// <returns>A hash consistent with <see cref="Equals(ValueObject)"/>.</returns>
    /// <remarks>
    /// Caching is safe because a value object is immutable: its components cannot change after
    /// construction, so the first answer stays the right one.
    /// </remarks>
    public sealed override int GetHashCode()
    {
        if (_cachedHashCode.HasValue)
        {
            return _cachedHashCode.Value;
        }

        var hash = new HashCode();
        foreach (var component in GetEqualityComponents())
        {
            hash.Add(component);
        }

        _cachedHashCode = hash.ToHashCode();
        return _cachedHashCode.Value;
    }

    /// <summary>Compares two value objects by their components.</summary>
    /// <param name="left">The left operand, possibly <see langword="null"/>.</param>
    /// <param name="right">The right operand, possibly <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both are <see langword="null"/> or equal by value.</returns>
    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        return Equals(left, right);
    }

    /// <summary>
    /// Compares two value objects by their components.
    /// </summary>
    /// <param name="left">The left operand, possibly <see langword="null"/>.</param>
    /// <param name="right">The right operand, possibly <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when they differ by value.</returns>
    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return !Equals(left, right);
    }
}
