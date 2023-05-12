using BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate;

namespace BLRefactoring.DDD.Infrastructure.Repositories;

public class TrainerRepository : ITrainerRepository
{
    public Task<Trainer> GetByIdAsync(Guid id)
    {
        throw new NotImplementedException();
    }
}
