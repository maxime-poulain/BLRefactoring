using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrainingHub.Shared.Application.Accounts;

namespace TrainingHub.Shared.Infrastructure.ThirdParty.Identity;

/// <summary>
/// The verification credential's store: mints from the system's entropy, keeps only a digest,
/// and redeems with one guarded delete.
/// </summary>
/// <remarks>
/// <see cref="PasswordResetTokenStore"/>'s design with the deviations ADR 0090 records. The token
/// is 256 bits from <see cref="RandomNumberGenerator"/> and only its SHA-256 digest is written,
/// unchanged. What differs: no expiry travels with the row, because the credential has none —
/// latest-only and the account's own cascade bound its life instead. And redemption looks the row
/// up <em>by digest</em> rather than by account, because the emailed link carries the token and
/// nothing else; the reset store's fixed-time compare defends a credential that takes an account
/// over, while this one proves a click, and an indexed lookup over 256-bit digests gives a prober
/// nothing a straight refusal would not.
/// </remarks>
public sealed class EmailVerificationTokenStore(
    UserManager<IdentityUser<Guid>> userManager,
    TrainingIdentityDbContext identityContext,
    IOptions<EmailVerificationOptions> options,
    TimeProvider timeProvider) : IEmailVerificationTokenStore
{
    /// <summary>
    /// How much entropy a token carries: 32 bytes, 256 bits.
    /// </summary>
    private const int TokenSizeInBytes = 32;

    /// <inheritdoc />
    public async Task<EmailVerificationInvitation?> IssueAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);

        // Identity types the address as nullable; this application never registers an account
        // without one, so an address-less row is treated the same as an absent account — there
        // is nowhere to deliver a link, and nothing is minted for it.
        if (user is null || user.Email is null || user.EmailConfirmed)
        {
            return null;
        }

        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenSizeInBytes));
        var issuedOnUtc = timeProvider.GetUtcNow().UtcDateTime;

        var current = await identityContext.Set<EmailVerificationToken>()
            .FirstOrDefaultAsync(candidate => candidate.UserId == user.Id, cancellationToken)
            .ConfigureAwait(false);

        if (current is null)
        {
            identityContext.Add(new EmailVerificationToken(user.Id, Digest(token), issuedOnUtc));
        }
        else
        {
            current.Reissue(Digest(token), issuedOnUtc);
        }

        // Two requests racing this insert can both read an absent row and one of them will lose
        // the primary key. That failure surfaces to the outbox worker, whose retry re-runs the
        // whole issue a moment later and finds the winner's row to replace — the invariant holds
        // either way, and the loser's email is simply the one that arrives second (ADR 0084).
        await identityContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var link = $"{options.Value.LinkBaseAddress.TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(token)}";

        // Read from the same account the mint just loaded, so the invitation answers who to write
        // to and in what language in one breath (ADR 0091). A null here means the account stated
        // no preference, and the composer is what turns that into the default.
        var language = await LanguageOfAsync(user.Id, cancellationToken).ConfigureAwait(false);

        return new EmailVerificationInvitation(link, user.UserName ?? user.Email, user.Email, language);
    }

    /// <inheritdoc />
    public async Task<Guid?> TryRedeemAsync(string token, CancellationToken cancellationToken = default)
    {
        var digest = Digest(token);

        // The read names the account, the delete decides. Two racing clicks both find the row;
        // only one delete removes it, and the loser answers null — the race settles at the
        // database, exactly as the reset store's redemption does (ADR 0084, ADR 0090).
        var current = await identityContext.Set<EmailVerificationToken>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.TokenHash == digest, cancellationToken)
            .ConfigureAwait(false);

        if (current is null)
        {
            return null;
        }

        var redeemed = await identityContext.Set<EmailVerificationToken>()
            .Where(candidate => candidate.UserId == current.UserId && candidate.TokenHash == digest)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return redeemed == 1 ? current.UserId : null;
    }

    /// <summary>
    /// The digest of a token as this store writes and compares it.
    /// </summary>
    /// <remarks>
    /// Hashed as the characters the visitor's request carries rather than as decoded bytes, so a
    /// malformed token digests to something that matches nothing instead of throwing — every
    /// wrong token walks the same path as a valid-looking one.
    /// </remarks>
    /// <summary>The language the account chose, or <see langword="null"/> when it chose none.</summary>
    /// <remarks>
    /// One indexed read on a primary key, on the same context the credential was just written
    /// through. The fallback is not applied here: the composer owns it, because it is the one
    /// corner allowed to name the list of languages (ADR 0091).
    /// </remarks>
    private Task<string?> LanguageOfAsync(Guid userId, CancellationToken cancellationToken) =>
        identityContext.Set<AccountLanguage>()
            .AsNoTracking()
            .Where(preference => preference.UserId == userId)
            .Select(preference => preference.Language)
            .FirstOrDefaultAsync(cancellationToken);

    private static byte[] Digest(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
