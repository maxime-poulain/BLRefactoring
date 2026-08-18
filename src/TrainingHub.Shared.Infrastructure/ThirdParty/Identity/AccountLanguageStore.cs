using Microsoft.EntityFrameworkCore;
using TrainingHub.Shared.Application.Accounts;

namespace TrainingHub.Shared.Infrastructure.ThirdParty.Identity;

/// <inheritdoc />
/// <remarks>
/// The two credential stores' shape: a writing port into the Identity store, saving through that
/// context rather than through the unit of work, which owns the business store alone (ADR 0084,
/// ADR 0091).
/// <para>
/// Read-then-write rather than a raw upsert, because two calls for one account cannot race in
/// practice — a person states their language from one session at a time — and because the row's
/// primary key would settle it at the database if they ever did.
/// </para>
/// </remarks>
public sealed class AccountLanguageStore(TrainingIdentityDbContext identityContext) : IAccountLanguageStore
{
    /// <inheritdoc />
    public async Task SetAsync(Guid userId, string language, CancellationToken cancellationToken = default)
    {
        var current = await identityContext.Set<AccountLanguage>()
            .FirstOrDefaultAsync(preference => preference.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (current is null)
        {
            identityContext.Add(new AccountLanguage(userId, language));
        }
        else
        {
            current.Choose(language);
        }

        await identityContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
