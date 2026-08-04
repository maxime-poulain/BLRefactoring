namespace TrainingHub.Shared.Application.IntegrationEvents;

/// <summary>
/// A business fact that leaves this bounded context, serialized into the outbox and delivered to
/// consumers after — and only after — the transaction that produced it has committed.
/// </summary>
/// <remarks>
/// Deliberately empty, and deliberately inheriting nothing. A domain event is an in-process
/// notification and may lean on the messaging library; an integration event is a message that is
/// serialized, stored and read back later, possibly by another process, so it must not carry an
/// in-process contract (ADR 0002). Keeping <c>Mediator.INotification</c> off this interface also
/// keeps <c>IMediator.Publish</c> unable to accept an integration event: the pre-commit bus and the
/// post-commit outbox cannot be confused, because the confusion does not compile. The worker that
/// delivers these messages gets its own handler contract instead (ADR 0024).
/// </remarks>
public interface IIntegrationEvent
{
}
