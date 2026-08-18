using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.Identity;
using Microsoft.EntityFrameworkCore;

namespace TrainingHub.Shared.Infrastructure.Repositories;

/// <inheritdoc />
/// <remarks>
/// The one domain-port adapter that opens two stores, and the reason it is not a repository
/// method: the trainer is a row of <see cref="TrainingContext"/> and the proof a column of
/// <see cref="TrainingIdentityDbContext"/>, and neither has ever been taught about the other —
/// the same separation <c>TrainerAccountQuery</c> pays for with two round-trips, charged here
/// once per creation or transfer (ADR 0090). Nothing is tracked: this answers a question, and a
/// change tracker has no business holding rows nobody is going to change.
/// </remarks>
public sealed class TrainerVerification(
    TrainingContext trainingContext,
    TrainingIdentityDbContext identityContext)
    : ITrainerVerification
{
    /// <inheritdoc />
    public async Task<bool> IsVerifiedAsync(TrainerId trainerId, CancellationToken cancellationToken = default)
    {
        var trainer = await trainingContext.Trainers
            .AsNoTracking()
            .Where(candidate => candidate.Id == trainerId)
            .Select(candidate => new { candidate.UserId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (trainer is null)
        {
            return false;
        }

        var userId = trainer.UserId.Value;

        return await identityContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId && user.EmailConfirmed, cancellationToken)
            .ConfigureAwait(false);
    }
}
