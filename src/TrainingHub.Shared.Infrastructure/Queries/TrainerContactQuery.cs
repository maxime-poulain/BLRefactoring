using Microsoft.EntityFrameworkCore;
using TrainingHub.Shared.Application.Accounts;
using TrainingHub.Shared.Application.Queries;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;

namespace TrainingHub.Shared.Infrastructure.Queries;

/// <inheritdoc />
/// <remarks>
/// One store, where <see cref="TrainerAccountQuery"/> opens two: the address a trainer publishes
/// is a column of the trainer's own row, and nothing here crosses into Identity. That asymmetry is
/// the seam ADR 0082 relies on — this adapter cannot read the account address even by accident,
/// because it never opens the context holding it, and the language it now also answers arrives
/// through a port that returns a culture code and nothing else (ADR 0091).
/// <para>
/// Nothing is tracked: this runs in the scope the delivery worker opened, and that change tracker
/// has no business holding a trainer nobody is going to change.
/// </para>
/// </remarks>
public sealed class TrainerContactQuery(
    TrainingContext trainingContext,
    IAccountLanguageQuery languages) : ITrainerContactQuery
{
    /// <inheritdoc />
    public async Task<TrainerContactDto?> GetByTrainerIdAsync(
        Guid trainerId,
        CancellationToken cancellationToken = default)
    {
        var id = TrainerId.Create(trainerId);

        var trainer = await trainingContext.Trainers
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new
            {
                candidate.ContactEmail.FullAddress,
                candidate.Name.Firstname,
                candidate.Name.Lastname,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (trainer is null)
        {
            return null;
        }

        // Asked rather than joined: the preference is a row of the Identity store, and opening
        // that context here is exactly what the remark above says this adapter must never do. The
        // port answers a culture code and can answer nothing else (ADR 0082, ADR 0091).
        var language = await languages.ByTrainerIdAsync(trainerId, cancellationToken).ConfigureAwait(false);

        return new TrainerContactDto(
            trainer.FullAddress,
            trainer.Firstname,
            trainer.Lastname,
            language);
    }
}
