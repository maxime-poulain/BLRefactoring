namespace BLRefactoring.Shared.Common;

/// <summary>
/// A fact this system publishes outside the transaction that produced it: an email to send,
/// an index to refresh, a message to put on a bus.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately distinct from <see cref="IDomainEvent"/>, and deliberately free of any
/// dispatcher dependency — <see cref="IDomainEvent"/> derives from Mediator's
/// <c>INotification</c>, which is fine for something dispatched in-process a microsecond after
/// it is raised, but wrong for something that is written to a table and read back later,
/// possibly by a different deployment.
/// </para>
/// <para>
/// An integration event is a contract with the future, so its shape is chosen for stability
/// rather than for expressiveness. Strongly-typed identifiers are welcome: their only rule —
/// an identifier cannot be empty — will never change. Value objects carrying business rules
/// are not: a message written today must not stop deserialising because a length limit moved
/// tomorrow. Those are flattened to primitives by whoever creates the event.
/// </para>
/// </remarks>
public interface IIntegrationEvent
{
    /// <summary>
    /// When the fact happened, as opposed to when it is published.
    /// </summary>
    DateTimeOffset OccurredOn { get; }
}
