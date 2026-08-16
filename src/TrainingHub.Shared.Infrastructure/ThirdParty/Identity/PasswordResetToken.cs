namespace TrainingHub.Shared.Infrastructure.ThirdParty.Identity;

/// <summary>
/// The one reset credential an account can have outstanding, stored as a digest.
/// </summary>
/// <remarks>
/// Not an aggregate and not a domain concept: recovery belongs to the Identity &amp; Access context,
/// which this repository buys rather than models, so its invariants are written in this context's
/// own idiom — a table shape rather than a behavior method (ADR 0084). Three of them live here:
/// <list type="bullet">
/// <item><description>
/// <b>Only the latest credential exists.</b> <see cref="UserId"/> is the primary key, so a second
/// live credential for the same account is unrepresentable — issuing replaces the row instead of
/// adding one.
/// </description></item>
/// <item><description>
/// <b>Nothing secret is at rest.</b> <see cref="TokenHash"/> is the SHA-256 digest of the token
/// the email carries, and the token itself is 256 bits from the system's entropy — a copy of this
/// table forges nothing.
/// </description></item>
/// <item><description>
/// <b>Consumption is deletion.</b> A redeemed credential does not exist, which is a stronger
/// statement than a flag: there is no consumed row to mishandle, and an expired row is inert
/// until the next request replaces it.
/// </description></item>
/// </list>
/// </remarks>
public sealed class PasswordResetToken
{
    /// <summary>
    /// How long a reset link stays valid once issued.
    /// </summary>
    /// <remarks>
    /// A requirement rather than a knob, which is why it is a constant and not configuration:
    /// every mention of the window — the store that stamps it, the email that announces it, the
    /// page copy that repeats it — reads this one field, and an architecture rule pins the value.
    /// </remarks>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Initializes the credential row for an account.
    /// </summary>
    /// <param name="userId">The account the credential belongs to.</param>
    /// <param name="tokenHash">The SHA-256 digest of the raw token.</param>
    /// <param name="createdOnUtc">When the credential was issued.</param>
    /// <param name="expiresOnUtc">When the credential stops being redeemable.</param>
    public PasswordResetToken(Guid userId, byte[] tokenHash, DateTime createdOnUtc, DateTime expiresOnUtc)
    {
        UserId = userId;
        TokenHash = tokenHash;
        CreatedOnUtc = createdOnUtc;
        ExpiresOnUtc = expiresOnUtc;
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
    /// When this credential stops being redeemable, exactly <see cref="Lifetime"/> after issue.
    /// </summary>
    public DateTime ExpiresOnUtc { get; private set; }

    /// <summary>
    /// Replaces the credential in place: the previous link dies the moment a new one is issued.
    /// </summary>
    /// <param name="tokenHash">The digest of the freshly minted token.</param>
    /// <param name="createdOnUtc">When the replacement was issued.</param>
    /// <param name="expiresOnUtc">When the replacement stops being redeemable.</param>
    public void Reissue(byte[] tokenHash, DateTime createdOnUtc, DateTime expiresOnUtc)
    {
        TokenHash = tokenHash;
        CreatedOnUtc = createdOnUtc;
        ExpiresOnUtc = expiresOnUtc;
    }
}
