namespace TrainingHub.Shared.Application.Accounts;

/// <summary>
/// What the verification email carries for one account: the finished link, the name to greet,
/// and the address to deliver to.
/// </summary>
/// <remarks>
/// No lifetime, unlike <see cref="PasswordResetInvitation"/>, because the credential has none:
/// a verification link stays redeemable until a newer link replaces it or the account leaves
/// (ADR 0090). An email announcing a window the database does not enforce would be a lie.
/// The address rides here rather than on the integration event for the reason ADR 0056 gives:
/// resolve it when the notice is sent, from the account the mint just read — an address copied
/// onto an outbox row would go stale the moment the account's changed.
/// </remarks>
/// <param name="Link">The absolute address of the verification page, the raw token in its query.</param>
/// <param name="Username">The account's username, for the message's greeting.</param>
/// <param name="EmailAddress">The account's email address, the one the link must prove.</param>
/// <param name="Language">
/// The language the account reads in, or <see langword="null"/> when it stated none. It rides
/// beside the address because that is where ADR 0091 puts it: the mint has just read the account,
/// so it answers both facts about its owner in one breath.
/// </param>
public sealed record EmailVerificationInvitation(
    string Link,
    string Username,
    string EmailAddress,
    string? Language);

/// <summary>
/// Issues and redeems the one email-verification credential an account can hold.
/// </summary>
/// <remarks>
/// The second writing port into the Identity store, in <see cref="IPasswordResetTokenStore"/>'s
/// exact mold and deliberately not merged with it: the two credentials share primitives — entropy,
/// digest, latest-only, one guarded delete — and share no meaning, and a port that served both
/// would put a password-granting credential and an address-proving one behind one door (ADR 0090).
/// <para>
/// The raw token crosses this boundary only inside <see cref="EmailVerificationInvitation.Link"/>,
/// on its way into the email. At rest there is nothing but a SHA-256 digest.
/// </para>
/// </remarks>
public interface IEmailVerificationTokenStore
{
    /// <summary>
    /// Mints a fresh credential for an account, replacing any earlier one.
    /// </summary>
    /// <param name="userId">The account asked to prove its address.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// The invitation to email, or <see langword="null"/> when the account is gone or has already
    /// proven its address — the caller sends nothing and says nothing, which both absorbs a
    /// resend that lost a race with the click and keeps the outbox retry idempotent (ADR 0090).
    /// </returns>
    Task<EmailVerificationInvitation?> IssueAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumes the credential a token names, exactly once.
    /// </summary>
    /// <remarks>
    /// Found by digest rather than by account, because the emailed link carries the token and
    /// nothing else — an identifier in the URL would put the account into browser histories for
    /// no gain, and 256 bits of entropy make the digest as selective as any key (ADR 0090, the
    /// one deviation from the reset store's fixed-time compare, recorded there). Consumption is
    /// one guarded delete, so two racing clicks settle at the database: one wins the row, the
    /// other learns it lost and changes nothing.
    /// </remarks>
    /// <param name="token">The raw token as the verification page received it.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// The account whose credential this call consumed, or <see langword="null"/> when the token
    /// names no live credential — unknown, superseded, already spent, or malformed alike.
    /// </returns>
    Task<Guid?> TryRedeemAsync(string token, CancellationToken cancellationToken = default);
}
