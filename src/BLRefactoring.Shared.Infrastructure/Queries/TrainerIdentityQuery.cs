using BLRefactoring.Shared.Application.Queries;
using BLRefactoring.Shared.Domain;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.Shared.Infrastructure.Queries;

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
