namespace BLRefactoring.Shared.Common;

/// <summary>
/// Reacts to an integration event once the transaction that produced it has committed.
/// </summary>
/// <remarks>
/// Unlike <see cref="IDomainEventHandler{TDomainEvent}"/>, a failure here does not roll anything
/// back — the business change is already durable. It only means the message stays pending and
/// will be retried, so a handler must tolerate running twice on the same message: the outbox
/// guarantees at-least-once delivery, never exactly-once.
/// </remarks>
public interface IIntegrationEventHandler<in TIntegrationEvent>
    where TIntegrationEvent : IIntegrationEvent
{
    Task HandleAsync(TIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Hands a deserialised integration event to whichever handlers are registered for its type.
/// </summary>
/// <remarks>
/// The seam that keeps the outbox processor ignorant of its consumers. Today they send emails
/// and refresh a search index; adding a message bus later means registering another handler,
/// not touching the processor.
/// </remarks>
public interface IIntegrationEventDispatcher
{
    Task DispatchAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
