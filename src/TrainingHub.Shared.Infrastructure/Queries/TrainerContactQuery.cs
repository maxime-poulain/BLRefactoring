using Microsoft.EntityFrameworkCore;
using TrainingHub.Shared.Application.Queries;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;

namespace TrainingHub.Shared.Infrastructure.Queries;

/// <inheritdoc />
/// <remarks>
/// One store and one round-trip, where <see cref="TrainerAccountQuery"/> needs two: the address a
/// trainer publishes is a column of the trainer's own row, and nothing here has to cross into
/// Identity. That asymmetry is the seam ADR 0082 relies on — this adapter cannot read the account
/// address even by accident, because it never opens the context holding it.
/// <para>
/// Nothing is tracked: this runs in the scope the delivery worker opened, and that change tracker
/// has no business holding a trainer nobody is going to change.
/// </para>
/// </remarks>
public sealed class TrainerContactQuery(TrainingContext trainingContext) : ITrainerContactQuery
{
    /// <inheritdoc />
    public async Task<TrainerContactDto?> GetByTrainerIdAsync(
        Guid trainerId,
        CancellationToken cancellationToken = default)
    {
        var id = TrainerId.Create(trainerId);

        return await trainingContext.Trainers
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new TrainerContactDto(
                candidate.ContactEmail.FullAddress,
                candidate.Name.Firstname,
                candidate.Name.Lastname))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
