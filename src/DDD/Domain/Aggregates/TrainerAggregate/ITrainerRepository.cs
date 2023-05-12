using BLRefactoring.Shared.Common;

namespace BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate;

public interface ITrainerRepository : IRepository<Trainer>
{
    public Task<Trainer> GetByIdAsync(Guid id);
}
