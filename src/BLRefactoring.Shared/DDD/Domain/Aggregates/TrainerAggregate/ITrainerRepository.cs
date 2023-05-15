using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate;

public interface ITrainerRepository : IRepository<Trainer>
{
    public ValueTask<Trainer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(Trainer trainer, CancellationToken cancellationToken = default);
    Task<List<Trainer>> GetAllAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(Trainer trainer, CancellationToken cancellationToken = default);
}
