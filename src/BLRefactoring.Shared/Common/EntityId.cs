using System.Linq.Expressions;
using System.Reflection;

namespace BLRefactoring.Shared.Common;

/// <summary>
/// Represents the identifier of an <see cref="Entity"/>.
/// The value of the identifier is a <see cref="Guid"/>.
/// A <see cref="EntityId{TEntityId}"/> is a ValueObject,
/// even though it does not inherit from <see cref="ValueObject"/>.
/// </summary>
/// <typeparam name="TEntityId">The actual type of the id.</typeparam>
public abstract class EntityId<TEntityId> : EntityId<TEntityId, Guid>
    where TEntityId : EntityId<TEntityId, Guid>
{
    protected EntityId(Guid value) : base(value)
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
    public static TEntityId Generate() => Create(Guid.NewGuid());
}

/// <summary>
/// Represents the identifier of a <see cref="EntityId{TEntityId,TValue}"/>.
/// An <see cref="EntityId{TEntityId,TValue}"/> can have any kind of <typeparamref name="TValue"/>
/// as long as it is a value type.
/// <see cref="EntityId{TEntityId,TValue}"/> is a ValueObject,
/// even though it does not inherit from <see cref="ValueObject"/>.
/// </summary>
/// <remarks>
/// <see cref="Create"/> (and <c>Generate</c> for <see cref="Guid"/>-based ids) is the
/// single construction path: it validates the value and instantiates the derived type
/// through its non-public constructor via a cached compiled factory. Derived id types
/// declare a private constructor taking <typeparamref name="TValue"/> and chaining the
/// base constructor, which makes bare instantiation (<c>new TrainerId()</c>) a compile
/// error and an empty identifier unrepresentable.
/// </remarks>
/// <typeparam name="TEntityId">The derived type that inherits from <see cref="EntityId{TEntityId,TValue}"/></typeparam>
/// <typeparam name="TValue">The type of the value of the id.</typeparam>
public abstract class EntityId<TEntityId, TValue> :
    IEquatable<EntityId<TEntityId, TValue>>,
    IComparable,
    IComparable<EntityId<TEntityId, TValue>>
    where TEntityId : EntityId<TEntityId, TValue>
    where TValue : struct, IComparable<TValue>, IEquatable<TValue>
{
    private static readonly Func<TValue, TEntityId> Factory = BuildFactory();

    /// <summary>
    /// Gets the unique identifier value of the <see cref="Entity{TEntityId}"/>.
    /// The value is of type <typeparamref name="TValue"/>.
    /// </summary>
    public TValue Value { get; }

    /// <summary>
    /// Initializes the identifier with a non-default value.
    /// </summary>
    /// <param name="value">The value of the identifier.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value"/> is the default value of
    /// <typeparamref name="TValue"/> (e.g. <see cref="Guid.Empty"/>).
    /// </exception>
    protected EntityId(TValue value)
    {
        if (value.Equals(default))
        {
            throw new ArgumentException(
                $"A {typeof(TEntityId).Name} cannot be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Creates a new instance of the <typeparamref name="TEntityId"/> class
    /// with the specified <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value for the new instance. Must not be the default value.</param>
    /// <returns>
    /// A new instance of the <typeparamref name="TEntityId"/> class
    /// with the specified <paramref name="value"/>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value"/> is the default value of <typeparamref name="TValue"/>.
    /// </exception>
    public static TEntityId Create(TValue value) => Factory(value);

    /// <summary>
    /// Explicitly converts a value into a <typeparamref name="TEntityId"/> with the
    /// same validation as <see cref="Create"/>. C# requires the enclosing type as the
    /// conversion target, so the operator returns the base type; a cast such as
    /// <c>(TrainerId)guid</c> combines this conversion with a reference downcast —
    /// safe, since <see cref="Create"/> builds the derived type.
    /// </summary>
    public static explicit operator EntityId<TEntityId, TValue>(TValue value) => Create(value);

    private static Func<TValue, TEntityId> BuildFactory()
    {
        var constructor = typeof(TEntityId).GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            [typeof(TValue)],
            modifiers: null);

        if (constructor is null)
        {
            throw new InvalidOperationException(
                $"{typeof(TEntityId).Name} must declare a constructor taking a single " +
                $"{typeof(TValue).Name} and chaining the base constructor.");
        }

        var value = Expression.Parameter(typeof(TValue), "value");
        return Expression
            .Lambda<Func<TValue, TEntityId>>(Expression.New(constructor, value), value)
            .Compile();
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

        if (this.GetType() != obj.GetType())
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

    public static bool operator ==(
        EntityId<TEntityId, TValue>? left,
        EntityId<TEntityId, TValue>? right)
        => Equals(left, right);

    public static bool operator !=(
        EntityId<TEntityId, TValue>? left,
        EntityId<TEntityId, TValue>? right)
        => !Equals(left, right);
}
