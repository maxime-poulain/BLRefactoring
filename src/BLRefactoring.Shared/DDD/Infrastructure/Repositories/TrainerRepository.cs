using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.DDD.Infrastructure.Repositories.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.Shared.DDD.Infrastructure.Repositories;

public class TrainerRepository : ITrainerRepository
{
    private readonly TrainingContext _trainingContext;

    public TrainerRepository(TrainingContext trainingContext)
    {
        _trainingContext = trainingContext;
    }

    public async ValueTask<Trainer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _trainingContext.Trainers
            .FindAsync(new object?[] { id }, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SaveAsync(Trainer trainer, CancellationToken cancellationToken = default)
    {
        if (trainer.IsTransient())
        {
            await _trainingContext.Trainers.AddAsync(trainer, cancellationToken);
        }
        else
        {
            _trainingContext.Trainers.Update(trainer);
        }
        await _trainingContext.SaveChangesAsync(cancellationToken);
    }

    public Task<List<Trainer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Owned Entities are by default included in the query.
        // However, we are explicitly including them here for the sake of clarity.
        return _trainingContext.Trainers
            .Include(trainer => trainer.Email)
            .Include(trainer => trainer.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteAsync(Trainer trainer, CancellationToken cancellationToken = default)
    {
        _trainingContext.Remove(trainer);
        await _trainingContext.SaveChangesAsync(cancellationToken);
    }
}
