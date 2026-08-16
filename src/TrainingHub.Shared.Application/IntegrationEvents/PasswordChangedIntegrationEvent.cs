namespace TrainingHub.Shared.Application.IntegrationEvents;

/// <summary>
/// An account's password was changed — the fact, committed with the change itself.
/// </summary>
/// <remarks>
/// Published by the recovery flow inside the same transaction that rewrote the credential, so the
/// notice reaches exactly the accounts whose password really changed (ADR 0084). The address and
/// username travel on the fact because the fact is <em>about</em> that account — the same
/// argument that puts the contact address on <see cref="TrainerCreatedIntegrationEvent"/> — and
/// its consumer is the owner's alarm bell: a password changed by somebody else is learned from
/// this message, while the reset link that did it sits in the same mailbox.
/// </remarks>
/// <param name="UserId">The account whose password changed.</param>
/// <param name="Email">The account's own address, where the notice goes.</param>
/// <param name="Username">The account's username, for the message's greeting.</param>
public sealed record PasswordChangedIntegrationEvent(Guid UserId, string Email, string Username) : IIntegrationEvent;
