namespace TrainingHub.Shared.Application.IntegrationEvents;

/// <summary>
/// Somebody asked to reset the password of the account behind an address — the fact, exactly as
/// the form received it.
/// </summary>
/// <remarks>
/// Like the contact fact, this one answers to no aggregate: asking to recover an account changes
/// nothing about a trainer, so the outbox row is the record, committed by the endpoint itself
/// (ADR 0082's shape, one context over). Publishing it is also the anti-enumeration mechanism of
/// ADR 0084: the request path does the same work — one row, one commit, one 202 — whether or not
/// the address names an account, and the lookup happens later, in the delivery worker, where no
/// caller can time it.
/// <para>
/// <b>The reset token is deliberately absent, and cannot be present:</b> it does not exist yet.
/// It is minted at delivery, inside the consumer, so the one secret in this flow never touches
/// the outbox table, whose payloads are plaintext and retained (ADR 0024, ADR 0084).
/// </para>
/// </remarks>
/// <param name="Email">The address the visitor asked to recover, known account or not.</param>
public sealed record PasswordResetRequestedIntegrationEvent(string Email) : IIntegrationEvent;
