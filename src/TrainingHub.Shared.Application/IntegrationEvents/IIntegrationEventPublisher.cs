namespace TrainingHub.Shared.Application.IntegrationEvents;

/// <summary>
/// Publishes an integration event to whoever consumes this bounded context's facts.
/// </summary>
/// <remarks>
/// "Publish" promises hand-off, not delivery: the implementation stages the event in the
/// transactional outbox, inside the same unit of work as the state change that justified it, and a
/// worker delivers it after the commit — at-least-once, settled per consumer in a ledger keyed by
/// the message identifier (ADR 0034). The port lives in the application layer because translating a domain event into an
/// integration event is an application decision (ADR 0002), and because the application layer may
/// not touch the persistence machinery that actually writes the row
/// (<c>TheApplicationLayer_KnowsNeitherPersistenceNorHttp</c>).
/// </remarks>
public interface IIntegrationEventPublisher
{
    /// <summary>
    /// Stages <paramref name="integrationEvent"/> for delivery, atomically with the unit of work
    /// in progress.
    /// </summary>
    /// <param name="integrationEvent">The fact to publish.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
