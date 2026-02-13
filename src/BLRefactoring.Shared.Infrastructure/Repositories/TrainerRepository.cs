using BLRefactoring.Shared.Domain;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.Shared.Infrastructure.Repositories;

public class TrainerRepository(TrainingContext trainingContext) : ITrainerRepository
{
    public async ValueTask<Trainer?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await trainingContext.Trainers
            .FirstOrDefaultAsync(trainer => trainer.Id == id, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<Trainer?> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        return await trainingContext.Trainers
            .FirstOrDefaultAsync(trainer => trainer.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SaveAsync(Trainer trainer, CancellationToken cancellationToken = default)
    {
        if (trainer.IsTransient())
        {
            await trainingContext.Trainers.AddAsync(trainer, cancellationToken);
        }
        else
        {
            trainingContext.Trainers.Update(trainer);
        }

        await trainingContext.SaveChangesAsync(cancellationToken);
    }

    public Task<List<Trainer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Owned Entities are by default included in the query.
        // However, we are explicitly including them here for the sake of clarity.
        return trainingContext.Trainers
            .Include(trainer => trainer.Email)
            .Include(trainer => trainer.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteAsync(Trainer trainer, CancellationToken cancellationToken = default)
    {
        trainingContext.Remove(trainer);
        await trainingContext.SaveChangesAsync(cancellationToken);
    }
}
