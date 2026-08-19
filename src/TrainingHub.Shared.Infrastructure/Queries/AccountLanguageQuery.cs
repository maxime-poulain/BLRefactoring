using Microsoft.EntityFrameworkCore;
using TrainingHub.Shared.Application.Accounts;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.Identity;

namespace TrainingHub.Shared.Infrastructure.Queries;

/// <inheritdoc />
/// <remarks>
/// <see cref="TrainerAccountQuery"/>'s shape, for its reason: the preference is a row of
/// <see cref="TrainingIdentityDbContext"/> and the trainer a row of <see cref="TrainingContext"/>,
/// and neither store has ever been taught about the other. The first question needs one context;
/// the second pays the seam's two round-trips.
/// <para>
/// Nothing is tracked: both questions run where a fact is about to be published, and that change
/// tracker has no business holding a preference nobody is going to change.
/// </para>
/// </remarks>
public sealed class AccountLanguageQuery(
    TrainingContext trainingContext,
    TrainingIdentityDbContext identityContext)
    : IAccountLanguageQuery
{
    /// <inheritdoc />
    public Task<string?> ByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        identityContext.Set<AccountLanguage>()
            .AsNoTracking()
            .Where(preference => preference.UserId == userId)
            .Select(preference => preference.Language)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<string?> ByTrainerIdAsync(Guid trainerId, CancellationToken cancellationToken = default)
    {
        var id = TrainerId.Create(trainerId);

        var userId = await trainingContext.Trainers
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => candidate.UserId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return userId is null
            ? null
            : await ByUserIdAsync(userId.Value, cancellationToken).ConfigureAwait(false);
    }
}
