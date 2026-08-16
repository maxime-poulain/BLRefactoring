using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrainingHub.Shared.Application.Accounts;

namespace TrainingHub.Shared.Infrastructure.ThirdParty.Identity;

/// <summary>
/// The reset credential's store: mints from the system's entropy, keeps only a digest, and
/// redeems with one guarded delete.
/// </summary>
/// <remarks>
/// The design is ADR 0084's, and each choice is visible in one member. The token is 256 bits from
/// <see cref="RandomNumberGenerator"/>, so guessing one is not a plan. Only its SHA-256 digest is
/// written, so a copy of the database redeems nothing — which is also why the framework's own
/// data-protected reset tokens were not used: their key ring would itself be the secret at rest.
/// The row is found by its account and compared in fixed time, never looked up by digest, so the
/// lookup path leaks no timing about what the table holds. And redemption is a single delete
/// guarded by digest and expiry, so two racing redemptions settle at the database.
/// </remarks>
public sealed class PasswordResetTokenStore(
    UserManager<IdentityUser<Guid>> userManager,
    TrainingIdentityDbContext identityContext,
    IOptions<PasswordResetOptions> options,
    TimeProvider timeProvider) : IPasswordResetTokenStore
{
    /// <summary>
    /// How much entropy a token carries: 32 bytes, 256 bits.
    /// </summary>
    private const int TokenSizeInBytes = 32;

    /// <inheritdoc />
    public async Task<PasswordResetInvitation?> IssueAsync(
        string emailAddress,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(emailAddress).ConfigureAwait(false);

        if (user is null)
        {
            return null;
        }

        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenSizeInBytes));
        var issuedOnUtc = timeProvider.GetUtcNow().UtcDateTime;
        var expiresOnUtc = issuedOnUtc + PasswordResetToken.Lifetime;

        var current = await identityContext.Set<PasswordResetToken>()
            .FirstOrDefaultAsync(candidate => candidate.UserId == user.Id, cancellationToken)
            .ConfigureAwait(false);

        if (current is null)
        {
            identityContext.Add(new PasswordResetToken(user.Id, Digest(token), issuedOnUtc, expiresOnUtc));
        }
        else
        {
            current.Reissue(Digest(token), issuedOnUtc, expiresOnUtc);
        }

        // Two requests racing this insert can both read an absent row and one of them will lose
        // the primary key. That failure surfaces to the outbox worker, whose retry re-runs the
        // whole issue a moment later and finds the winner's row to replace — the invariant holds
        // either way, and the loser's email is simply the one that arrives second (ADR 0084).
        await identityContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var link = $"{options.Value.LinkBaseAddress.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}";

        return new PasswordResetInvitation(link, user.UserName ?? emailAddress, PasswordResetToken.Lifetime);
    }

    /// <inheritdoc />
    public async Task<bool> IsCurrentAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var current = await identityContext.Set<PasswordResetToken>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (current is null || current.ExpiresOnUtc <= timeProvider.GetUtcNow().UtcDateTime)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Digest(token), current.TokenHash);
    }

    /// <inheritdoc />
    public async Task<bool> TryRedeemAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var digest = Digest(token);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var redeemed = await identityContext.Set<PasswordResetToken>()
            .Where(candidate => candidate.UserId == userId
                && candidate.TokenHash == digest
                && candidate.ExpiresOnUtc > now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return redeemed == 1;
    }

    /// <summary>
    /// The digest of a token as this store writes and compares it.
    /// </summary>
    /// <remarks>
    /// Hashed as the characters the visitor's request carries rather than as decoded bytes, so a
    /// malformed token digests to something that matches nothing instead of throwing — every
    /// wrong token walks the same path as a valid-looking one.
    /// </remarks>
    private static byte[] Digest(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
