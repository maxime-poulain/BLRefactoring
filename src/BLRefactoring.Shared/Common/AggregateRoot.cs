namespace BLRefactoring.Shared.Common;

/// <summary>
/// Represents the root of an aggregate, which is a group of related entities and value objects
/// that form a consistency boundary for business rules and transactions.
/// Inherits from Entity and implements IAggregateRoot.
///
/// Example: In an e-commerce domain, an Order (Aggregate Root) may consist of OrderLines (Entities)
/// and Address (Value Objects). The Order entity is responsible for maintaining consistency
/// and managing the state of the entire aggregate.
/// </summary>
/// <typeparam name="TEntityId">The type of the unique identifier for the aggregate root entity.</typeparam>
/// <remarks>
/// For more information, see:
/// <list type="bullet">
/// <item><description><see href="https://martinfowler.com/bliki/DDD_Aggregate.html">Martin Fowler's DDD Aggregate</see></description></item>
/// <item><description><see href="https://docs.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/ddd-oriented-microservice">Microsoft's DDD-Oriented Microservice</see></description></item>
/// </list>
/// </remarks>
public abstract class AggregateRoot<TEntityId> : Entity<TEntityId>, IAggregateRoot
    where TEntityId : EntityId<TEntityId>, new()
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <inheritdoc/>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Raises a domain event. Only the aggregate's own behavior methods may raise
    /// events, so that every event is the outcome of a legitimate state transition.
    /// </summary>
    /// <param name="domainEvent">The domain event to raise.</param>
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Raises a collection of domain events. Only the aggregate's own behavior
    /// methods may raise events.
    /// </summary>
    /// <param name="domainEvents">The collection of domain events to raise.</param>
    protected void AddDomainEvents(IEnumerable<IDomainEvent> domainEvents)
    {
        _domainEvents.AddRange(domainEvents);
    }

    /// <inheritdoc/>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

/// <summary>
/// Marker interface for <see cref="AggregateRoot{TEntityId}"/>.
/// </summary>
public interface IAggregateRoot : IHasDomainEvents
{
}
