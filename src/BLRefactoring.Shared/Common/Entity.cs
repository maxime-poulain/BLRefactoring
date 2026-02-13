namespace BLRefactoring.Shared.Common;

/// <summary>
/// Represents an abstract entity with a unique identifier and audit properties.
/// </summary>
/// <typeparam name="TEntityId">The type of the unique identifier for the entity.</typeparam>
public abstract class Entity<TEntityId> : Entity, IAuditable
    where TEntityId : EntityId<TEntityId>, new()
{
    /// <summary>
    /// Gets the unique identifier for the entity.
    /// </summary>
    public virtual TEntityId Id { get; init; } = EntityId<TEntityId>.Generate();

    /// <summary>
    /// Gets or sets the date and time the entity was created.
    /// </summary>
    public DateTime CreatedOn { get;  }

    /// <summary>
    /// Gets or sets the date and time the entity was last modified, if any.
    /// </summary>
    public DateTime? ModifiedOn { get; }

    protected Entity()
    {
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
        return IsTransient() ? base.GetHashCode() : Id.GetHashCode();
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

    /*
     * DO NO MODIFY THIS.
     * `IsTransientMaterializationInterceptor` class intercept materialization
     * and uses tree expressions to set the private field to `false`.
     */
#pragma warning disable RCS1169 // Make field read-only.
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable S2933 // Fields that are only assigned in the constructor should be "readonly"
    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    private bool _isTransient = true;
#pragma warning restore S2933 // Fields that are only assigned in the constructor should be "readonly"
#pragma warning restore IDE0044 // Add readonly modifier
#pragma warning restore RCS1169 // Make field read-only.

    /// <summary>
    /// Determines if the underlying entity has already been persisted.
    /// </summary>
    /// <returns>
    /// Returns <see langword="true"/> if the entity has been persisted;
    /// <see langword="false"/> otherwise.
    /// </returns>
    public virtual bool IsTransient() => _isTransient;
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
public interface IHasDomainEvents
{
    /// <summary>
    /// Gets the domain events raised by this aggregate root.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents { get; }

    /// <summary>
    /// Adds a domain event to the aggregate root.
    /// </summary>
    /// <param name="domainEvent">The domain event to add.</param>
    void AddDomainEvent(IDomainEvent domainEvent);

    /// <summary>
    /// Adds a collection of domain events to the aggregate root.
    /// </summary>
    /// <param name="domainEvents">The collection of domain events to add.</param>
    void AddDomainEvents(IEnumerable<IDomainEvent> domainEvents);

    /// <summary>
    /// Clears the domain events raised by the aggregate root.
    /// </summary>
    void ClearDomainEvents();

    /// <summary>
    /// Removes a domain event from the aggregate root.
    /// </summary>
    /// <param name="domainEvent">The domain event to remove.</param>
    void RemoveDomainEvent(IDomainEvent domainEvent);
}
