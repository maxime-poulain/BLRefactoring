using BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.DDD.Infrastructure.Repositories.EfCore;

namespace BLRefactoring.DDD.Infrastructure.Repositories;

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
}
