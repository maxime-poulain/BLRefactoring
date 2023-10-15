namespace BLRefactoring.Shared.Common;

/// <summary>
/// Represents the identifier of an <see cref="Entity"/>.
/// The value of the identifier is a <see cref="Guid"/>.
/// A <see cref="EntityId{TEntityId}"/> is a ValueObject,
/// even though it does not inherit from <see cref="ValueObject"/>.
/// </summary>
/// <typeparam name="TEntityId">The actual type of the id.</typeparam>
public abstract class EntityId<TEntityId> : EntityId<TEntityId, Guid>
    where TEntityId : EntityId<TEntityId, Guid>, new()
{
    protected EntityId()
    {
    }

    /// <summary>
    /// Generates a new instance of a <typeparamref name="TEntityId"/> with an
    /// auto-generated <see cref="Guid"/>.
    /// </summary>
    /// <returns>
    /// A new instance of the <typeparamref name="TEntityId"/> class with an
    /// auto-generated <see cref="Guid"/>.
    /// </returns>
    public static TEntityId Default() => Create(Guid.NewGuid());
}

/// <summary>
/// Represents the identifier of a <see cref="EntityId{TEntityId,TValue}"/>.
/// An <see cref="EntityId{TEntityId,TValue}"/> can have any kind of <paramref name="TValue"/>
/// as long as it is a value type..
/// <see cref="EntityId{TEntityId,TValue}"/> is a ValueObject.
/// Even though it does not inherit from <see cref="ValueObject"/>.
/// </summary>
/// /// <typeparam name="TEntityId">The derived type that inherits from <see cref="EntityId{TEntityId,TValue}"/></typeparam>
/// <typeparam name="TValue">The type of the value of the id.</typeparam>
public abstract class EntityId<TEntityId, TValue> :
    IEquatable<EntityId<TEntityId, TValue>>,
    IComparable,
    IComparable<EntityId<TEntityId, TValue>>
    where TEntityId : EntityId<TEntityId, TValue>, new()
    where TValue : struct, IComparable<TValue>, IEquatable<TValue>
{
    /// <summary>
    /// Gets the unique identifier value of the <see cref="Entity{TEntityId}"/>.
    /// The value is of type <typeparamref name="TValue"/>.
    /// </summary>
    public TValue Value { get; protected init; }

    protected EntityId()
    {
    }

    protected EntityId(TValue value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new instance of the <typeparamref name="TEntityId"/> class
    /// with the specified <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value for the new instance.</param>
    /// <returns>
    /// A new instance of the <typeparamref name="TEntityId"/> class
    /// with the specified <paramref name="value"/>.
    /// </returns>
    public static TEntityId Create(TValue value)
    {
        return new() { Value = value };
    }

    public bool Equals(EntityId<TEntityId, TValue>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Value.Equals(other.Value);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return Equals((EntityId<TEntityId, TValue>)obj);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public int CompareTo(object? obj)
    {
        if (obj is TEntityId entityId)
        {
            return CompareTo(entityId);
        }

        throw new ArgumentException("Object must be of type " + nameof(TEntityId));
    }

    public int CompareTo(EntityId<TEntityId, TValue>? other)
    {
        if (other is null)
        {
            return 1;
        }

        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        return Value.CompareTo(other.Value);
    }

    public static implicit operator TValue(EntityId<TEntityId, TValue>? id) => id.Value;

    public static explicit operator EntityId<TEntityId, TValue>(TValue value)
        => new TEntityId() { Value = value };

    public static bool operator ==(
        EntityId<TEntityId, TValue>? left,
        EntityId<TEntityId, TValue>? right)
        => Equals(left, right);

    public static bool operator !=(
        EntityId<TEntityId, TValue>? left,
        EntityId<TEntityId, TValue>? right)
        => !Equals(left, right);
}
