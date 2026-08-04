namespace TrainingHub.Shared.Application.IntegrationEvents;

/// <summary>
/// Reacts to an integration event after the commit that made it a fact — the consuming side of the
/// outbox, invoked by the delivery worker.
/// </summary>
/// <remarks>
/// Deliberately this bounded context's own contract rather than the messaging library's: a domain
/// event handler runs inside the transaction on the in-process bus, an integration event handler
/// runs after it from a stored message, and keeping the two contracts apart keeps the two moments
/// impossible to confuse (ADR 0024, ADR 0025). Delivery is at-least-once, so an implementation is
/// idempotent or tolerates repetition; throwing is the protocol for "try again" — the worker
/// records the failure on the envelope and retries until the attempt budget is spent.
/// </remarks>
/// <typeparam name="TEvent">The integration event this handler consumes.</typeparam>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    /// <summary>
    /// Runs the reaction to a delivered fact.
    /// </summary>
    /// <param name="integrationEvent">The fact, deserialized from its envelope.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}
