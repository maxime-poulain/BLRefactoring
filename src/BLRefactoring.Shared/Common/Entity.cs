namespace BLRefactoring.Shared.Common;

/// <summary>
/// Represents an abstract entity with a unique identifier and audit properties.
/// </summary>
/// <typeparam name="TEntityId">The type of the unique identifier for the entity.</typeparam>
public abstract class Entity<TEntityId> : Entity, IAuditable
    where TEntityId : EntityId<TEntityId>
{
    /// <summary>
    /// Gets the unique identifier for the entity.
    /// </summary>
    public TEntityId Id { get; }

    /// <summary>
    /// Gets or sets the date and time the entity was created.
    /// </summary>
    public DateTime CreatedOn { get;  }

    /// <summary>
    /// Gets or sets the date and time the entity was last modified, if any.
    /// </summary>
    public DateTime? ModifiedOn { get; }

    /// <summary>
    /// Initializes the entity with its identity. The identifier is supplied by the
    /// caller — typically generated upfront by the layer issuing the creation
    /// command — so the entity is never observable without an identity, and callers
    /// know the primary key before any round-trip.
    /// </summary>
    /// <param name="id">The unique identifier of the entity.</param>
    protected Entity(TEntityId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        Id = id;
    }

    protected bool Equals(Entity<TEntityId> other)
    {
        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
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

        return Equals((Entity<TEntityId>)obj);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public static bool operator ==(Entity<TEntityId>? a, Entity<TEntityId>? b)
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

    public static bool operator !=(Entity<TEntityId> a, Entity<TEntityId> b)
    {
        return !(a == b);
    }
}

/// <summary>
/// Marker class for entities.
/// </summary>
public abstract class Entity
{
}

/// <summary>
/// Represents an <see cref="Entity"/> that is auditable.
/// </summary>
public interface IAuditable
{
    public DateTime CreatedOn { get; }
    public DateTime? ModifiedOn { get; }
}

/// <summary>
/// Represents an object that has a collection of <see cref="IDomainEvent"/>.
/// Usually the <see cref="IAggregateRoot"/> are the only model having <see cref="IDomainEvent"/>.
/// </summary>
/// <remarks>
/// The contract is deliberately read-and-clear only: raising events is the exclusive
/// privilege of the aggregate's own behavior methods (through the protected members of
/// <see cref="AggregateRoot{TEntityId}"/>), so that every event is the outcome of a
/// legitimate state transition. Consumers — typically the event-dispatching
/// infrastructure — can only read the pending events and clear them once collected.
/// </remarks>
public interface IHasDomainEvents
{
    /// <summary>
    /// Gets the domain events raised by this aggregate root.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents { get; }

    /// <summary>
    /// Clears the domain events raised by the aggregate root.
    /// </summary>
    void ClearDomainEvents();
}
