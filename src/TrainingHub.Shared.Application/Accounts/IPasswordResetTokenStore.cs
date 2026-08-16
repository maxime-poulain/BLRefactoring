namespace TrainingHub.Shared.Application.Accounts;

/// <summary>
/// What the reset email carries for one account: the finished link, the name to greet, and how
/// long the link lives.
/// </summary>
/// <param name="Link">The absolute address of the reset page, the raw token in its query.</param>
/// <param name="Username">The account's username, for the message's greeting.</param>
/// <param name="Lifetime">
/// How long the link stays valid — told by the store that stamped the expiry, so the email
/// announces the same window the database enforces.
/// </param>
public sealed record PasswordResetInvitation(string Link, string Username, TimeSpan Lifetime);

/// <summary>
/// Issues, checks and redeems the one password-reset credential an account can hold.
/// </summary>
/// <remarks>
/// The port through which recovery touches the Identity store, declared here because both of its
/// callers sit above the infrastructure: the outbox consumer that issues a credential when a
/// reset is requested, and the API's recovery flow that redeems one. It is a writing port held
/// by a post-commit consumer, which is the search indexer's shape and not the read ports' —
/// it writes a store the unit of work does not own, through a save of its own (ADR 0084).
/// <para>
/// The raw token crosses this boundary only inside <see cref="PasswordResetInvitation.Link"/>,
/// on its way into the email. At rest there is nothing but a SHA-256 digest, so no store this
/// port touches can leak a redeemable credential.
/// </para>
/// </remarks>
public interface IPasswordResetTokenStore
{
    /// <summary>
    /// Mints a fresh credential for the account behind an address, replacing any earlier one.
    /// </summary>
    /// <param name="emailAddress">The address the visitor asked to recover.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// The invitation to email, or <see langword="null"/> when no account listens at that
    /// address — the caller sends nothing and says nothing, which is the anti-enumeration
    /// posture the whole flow holds (ADR 0084).
    /// </returns>
    Task<PasswordResetInvitation?> IssueAsync(string emailAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Answers whether a token is the account's current, unexpired credential — a look, not a
    /// redemption.
    /// </summary>
    /// <remarks>
    /// The recovery flow peeks before it validates the new password, so a password the policy
    /// refuses never costs the visitor their link. Redemption is a separate, atomic step.
    /// </remarks>
    /// <param name="userId">The account the token claims to belong to.</param>
    /// <param name="token">The raw token as the reset page received it.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task<bool> IsCurrentAsync(Guid userId, string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumes the credential, exactly once.
    /// </summary>
    /// <remarks>
    /// One guarded delete: it removes the row only if it still matches this token and has not
    /// expired, and answers whether it was this call that removed it. Two racing redemptions
    /// therefore settle at the database — one wins the row, the other learns it lost and changes
    /// nothing (ADR 0084).
    /// </remarks>
    /// <param name="userId">The account the token claims to belong to.</param>
    /// <param name="token">The raw token as the reset page received it.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task<bool> TryRedeemAsync(Guid userId, string token, CancellationToken cancellationToken = default);
}
