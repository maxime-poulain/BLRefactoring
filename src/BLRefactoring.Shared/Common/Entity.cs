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
    public DateTime CreatedOn { get; }

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

    /// <summary>
    /// Compares two entities of this type by identifier.
    /// </summary>
    /// <param name="other">The entity to compare against.</param>
    /// <returns><see langword="true"/> when both carry the same identifier.</returns>
    /// <remarks>
    /// An entity is its identity, not its contents: a trainer whose name changed is the same
    /// trainer. That is the difference from <see cref="ValueObject"/>, which compares by value.
    /// </remarks>
    protected bool Equals(Entity<TEntityId> other)
    {
        return Id.Equals(other.Id);
    }

    /// <summary>
    /// Compares this entity with an arbitrary object.
    /// </summary>
    /// <param name="obj">The object to compare against.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="obj"/> is an entity of exactly this type
    /// carrying the same identifier.
    /// </returns>
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

    /// <summary>
    /// Returns the identifier's hash, so that equal entities hash alike.
    /// </summary>
    /// <returns>The hash code of <see cref="Id"/>.</returns>
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    /// <summary>Compares two entities by identifier.</summary>
    /// <param name="a">The left operand, possibly <see langword="null"/>.</param>
    /// <param name="b">The right operand, possibly <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both are <see langword="null"/> or share an identifier.</returns>
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

    /// <summary>
    /// Compares two entities by identifier.
    /// </summary>
    /// <param name="a">The left operand.</param>
    /// <param name="b">The right operand.</param>
    /// <returns><see langword="true"/> when they carry different identifiers.</returns>
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
    /// <summary>
    /// When the row behind this entity was first written.
    /// </summary>
    /// <remarks>
    /// Set by the persistence layer's interceptor rather than by any behaviour method, so the
    /// domain never has to be handed a clock.
    /// </remarks>
    DateTime CreatedOn { get; }

    /// <summary>
    /// When the row behind this entity was last changed, or <see langword="null"/> if it never was.
    /// </summary>
    DateTime? ModifiedOn { get; }
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
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    /// <summary>
    /// Clears the domain events raised by the aggregate root.
    /// </summary>
    void ClearDomainEvents();
}
