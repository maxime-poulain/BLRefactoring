namespace TrainingHub.Shared.Infrastructure.ThirdParty.Identity;

/// <summary>
/// The one verification credential an account can have outstanding, stored as a digest.
/// </summary>
/// <remarks>
/// <see cref="PasswordResetToken"/>'s shape minus the clock, and the difference is the decision
/// (ADR 0090): a verification link does not expire, because the mailbox is checked on the
/// owner's schedule and the credential proves nothing an attacker can spend — verifying somebody
/// else's account grants its owner publishing rights and the attacker nothing. What bounds its
/// life instead is written in the same idiom as the reset credential's invariants:
/// <list type="bullet">
/// <item><description>
/// <b>Only the latest credential exists.</b> <see cref="UserId"/> is the primary key, so asking
/// again replaces the row — every resend is a revocation of the previous link.
/// </description></item>
/// <item><description>
/// <b>Nothing secret is at rest.</b> <see cref="TokenHash"/> is the SHA-256 digest of the token
/// the email carries; a copy of this table forges nothing.
/// </description></item>
/// <item><description>
/// <b>Consumption is deletion.</b> A redeemed credential does not exist, and the account's row
/// dies with the account by cascade.
/// </description></item>
/// </list>
/// <see cref="CreatedOnUtc"/> stays although nothing expires on it: it is the operator's
/// forensics, and the one-column hedge that would make a future lifetime a rule change rather
/// than a migration.
/// </remarks>
public sealed class EmailVerificationToken
{
    /// <summary>
    /// Initializes the credential row for an account.
    /// </summary>
    /// <param name="userId">The account the credential belongs to.</param>
    /// <param name="tokenHash">The SHA-256 digest of the raw token.</param>
    /// <param name="createdOnUtc">When the credential was issued.</param>
    public EmailVerificationToken(Guid userId, byte[] tokenHash, DateTime createdOnUtc)
    {
        UserId = userId;
        TokenHash = tokenHash;
        CreatedOnUtc = createdOnUtc;
    }

    /// <summary>
    /// The account this credential belongs to — and the primary key, which is the single-credential
    /// invariant expressed as schema.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// The SHA-256 digest of the raw token. The token itself is never stored anywhere.
    /// </summary>
    public byte[] TokenHash { get; private set; }

    /// <summary>
    /// When this credential was issued.
    /// </summary>
    public DateTime CreatedOnUtc { get; private set; }

    /// <summary>
    /// Replaces the credential in place: the previous link dies the moment a new one is issued.
    /// </summary>
    /// <param name="tokenHash">The digest of the freshly minted token.</param>
    /// <param name="createdOnUtc">When the replacement was issued.</param>
    public void Reissue(byte[] tokenHash, DateTime createdOnUtc)
    {
        TokenHash = tokenHash;
        CreatedOnUtc = createdOnUtc;
    }
}
