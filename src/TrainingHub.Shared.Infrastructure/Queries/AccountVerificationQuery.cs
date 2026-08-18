using Microsoft.EntityFrameworkCore;
using TrainingHub.Shared.Application.Queries;
using TrainingHub.Shared.Infrastructure.ThirdParty.Identity;

namespace TrainingHub.Shared.Infrastructure.Queries;

/// <inheritdoc />
/// <remarks>
/// The Identity store alone: the fact is a column of <c>AspNetUsers</c>, the caller hands over
/// the user identifier their token already carries, and no trainer row is involved — which is
/// what separates this reader from <see cref="Repositories.TrainerVerification"/>, the domain's
/// adapter that starts from a <c>TrainerId</c> and has to open both stores (ADR 0090).
/// </remarks>
public sealed class AccountVerificationQuery(TrainingIdentityDbContext identityContext)
    : IAccountVerificationQuery
{
    /// <inheritdoc />
    /// <remarks>
    /// An empty identifier names no account and answers so, before the query runs: this reader
    /// sits on the authorization path, ahead of the validation pipeline that would have refused
    /// the request — the trap every boundary reader in this folder documents.
    /// </remarks>
    public async Task<bool> IsVerifiedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return false;
        }

        // AnyAsync over the pair rather than reading the account and comparing in memory: the
        // question is a bit, the answer is an index seek, and nothing here needs the row.
        return await identityContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId && user.EmailConfirmed, cancellationToken)
            .ConfigureAwait(false);
    }
}
