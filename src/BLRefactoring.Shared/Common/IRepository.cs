using BLRefactoring.Shared.Common.Specifications;

namespace BLRefactoring.Shared.Common;

/// <summary>
/// A generic repository pattern interface for managing aggregate roots in a domain-driven design (DDD) context,
/// as introduced by Eric Evans in "Domain-Driven Design: Tackling Complexity in the Heart of Software."
/// The <see cref="IRepository{TAggregate}"/> interface abstracts data access and storage logic, allowing the domain model
/// to focus on business rules, constraints, and logic, as described by Vaughn Vernon in "Implementing Domain-Driven Design."
/// It provides a collection-like interface for querying and persisting aggregate roots while enforcing the constraint
/// that it can only be used with types implementing the IAggregateRoot interface,which is consistent with the guidelines
/// presented by Scott Millett and Nick Tune in "Patterns, Principles, and Practices of Domain-Driven Design."
/// By implementing <see cref="IRepository{TAggregate}"/> for different aggregate roots in the system,
/// a consistent and flexible way to manage data access and storage is achieved, making it easier to maintain, evolve, and scale
/// the domain model while adhering to DDD principles.
/// </summary>
/// <typeparam name="TAggregate">The type of the aggregate root to be managed by the repository. The aggregate root serves as the entry point for interacting with an aggregate, enforcing its invariants and ensuring the internal state is consistent, as emphasized by Eric Evans in "Domain-Driven Design: Tackling Complexity in the Heart of Software." TAggregate must implement the IAggregateRoot interface.</typeparam>
public interface IRepository<TAggregate> where TAggregate : class, IAggregateRoot
{
    /// <summary>
    /// Retrieves a list of aggregates matching the given specification.
    /// </summary>
    /// <param name="spec">The specification defining the query criteria, ordering, and paging.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A list of aggregates matching the specification.</returns>
    Task<List<TAggregate>> GetAsync(ISpecification<TAggregate> spec, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether any aggregate matches the given specification.
    /// </summary>
    /// <param name="spec">The specification defining the query criteria.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if at least one aggregate matches; otherwise, <c>false</c>.</returns>
    Task<bool> AnyAsync(ISpecification<TAggregate> spec, CancellationToken cancellationToken = default);
}
