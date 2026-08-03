using TrainingHub.Shared.Application.Queries;
using TrainingHub.Shared.Domain;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace TrainingHub.Shared.Infrastructure.Queries;

/// <inheritdoc />
public sealed class TrainerIdentityQuery(TrainingContext trainingContext) : ITrainerIdentityQuery
{
    /// <inheritdoc />
    public async Task<TrainerIdentityDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var id = UserId.Create(userId);

        return await trainingContext.Trainers
            .Where(trainer => trainer.UserId == id)
            .Select(trainer => new TrainerIdentityDto(
                trainer.Id.Value,
                trainer.Name.Firstname,
                trainer.Name.Lastname))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
