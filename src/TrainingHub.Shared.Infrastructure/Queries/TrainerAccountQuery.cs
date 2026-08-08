using TrainingHub.Shared.Application.Queries;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.Identity;
using Microsoft.EntityFrameworkCore;

namespace TrainingHub.Shared.Infrastructure.Queries;

/// <inheritdoc />
/// <remarks>
/// The only read port here that opens two stores, and the reason it takes two contexts: the trainer
/// is a row of <see cref="TrainingContext"/> and the address a column of
/// <see cref="TrainingIdentityDbContext"/>, and neither has ever been taught about the other. Two
/// round-trips rather than one query is the price of that separation, charged once per notice.
/// <para>
/// Nothing is tracked, unlike its two siblings: this runs in the scope the delivery worker opened,
/// and that change tracker has no business holding a trainer nobody is going to change.
/// </para>
/// </remarks>
public sealed class TrainerAccountQuery(
    TrainingContext trainingContext,
    TrainingIdentityDbContext identityContext)
    : ITrainerAccountQuery
{
    /// <inheritdoc />
    public async Task<TrainerAccountDto?> GetByTrainerIdAsync(Guid trainerId, CancellationToken cancellationToken = default)
    {
        var id = TrainerId.Create(trainerId);

        var trainer = await trainingContext.Trainers
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new
            {
                candidate.UserId,
                candidate.Name.Firstname,
                candidate.Name.Lastname,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (trainer is null)
        {
            return null;
        }

        var userId = trainer.UserId.Value;

        var emailAddress = await identityContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.Email)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(emailAddress)
            ? null
            : new TrainerAccountDto(emailAddress, trainer.Firstname, trainer.Lastname);
    }
}
