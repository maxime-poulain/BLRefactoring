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
    where TEntityId : EntityId<TEntityId>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// Initializes the aggregate root with its identity.
    /// </summary>
    /// <param name="id">The unique identifier of the aggregate root.</param>
    protected AggregateRoot(TEntityId id) : base(id)
    {
    }

    /// <inheritdoc/>
    public byte[] RowVersion { get; private set; } = [];

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
    /// <summary>
    /// The version of the aggregate as it was read, maintained by the store.
    /// </summary>
    /// <remarks>
    /// The aggregate is the consistency boundary, so it is also the unit of
    /// concurrency control: one version guards the root and everything it owns.
    /// This is technical metadata rather than a business concept — it sits here
    /// next to <c>CreatedOn</c>/<c>ModifiedOn</c> for the same reason, and no
    /// business rule ever reads it. Callers compare it to decide whether the
    /// aggregate they are about to change is still the one they read.
    /// </remarks>
    byte[] RowVersion { get; }
}
