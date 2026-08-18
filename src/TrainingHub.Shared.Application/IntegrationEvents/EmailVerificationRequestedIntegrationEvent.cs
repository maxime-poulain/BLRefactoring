namespace TrainingHub.Shared.Application.IntegrationEvents;

/// <summary>
/// An account asked to prove its email address — at registration, or again from the resend
/// button — and the proving link should be mailed.
/// </summary>
/// <remarks>
/// The fourth fact whose endpoint commits its own outbox row (after the reset request, the
/// password change and the erasure): asking to be verified changes nothing about a trainer, so
/// the row is the record, published inside the registration's transaction or by the resend
/// endpoint's own save (ADR 0090). The verification token is deliberately absent for ADR 0084's
/// reason — it does not exist yet; it is minted at delivery, inside the consumer, so the secret
/// never touches the outbox table. The email address is absent too: the consumer resolves it
/// from the account at delivery time, the way every notice does (ADR 0056), so a row retried
/// tomorrow still writes to the address the account holds then.
/// <para>
/// It carried the requester's culture until ADR 0091, and no longer does. The address comes from
/// the invitation the mint hands back, so the language comes from there too — one authority
/// beats two that can disagree, and the account's stated preference is the one that survives a
/// row retried tomorrow.
/// </para>
/// </remarks>
/// <param name="UserId">The account that asked to prove its address.</param>
public sealed record EmailVerificationRequestedIntegrationEvent(Guid UserId) : IIntegrationEvent;
