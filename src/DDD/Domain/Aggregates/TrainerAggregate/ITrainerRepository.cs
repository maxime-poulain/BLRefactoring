using BLRefactoring.Shared.Common;

namespace BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate;

public interface ITrainerRepository : IRepository<Trainer>
{
    public ValueTask<Trainer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
