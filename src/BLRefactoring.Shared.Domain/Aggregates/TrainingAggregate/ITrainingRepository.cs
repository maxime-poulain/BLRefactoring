using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;

public interface ITrainingRepository : IRepository<Training>
{
    public Task<Training?> GetByIdAsync(TrainingId id, CancellationToken cancellationToken = default);
    Task SaveAsync(Training training, CancellationToken cancellationToken = default);
    Task<List<Training>> GetAllAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(Training training, CancellationToken cancellationToken = default);
    Task DeleteAsync(IEnumerable<Training> trainings, CancellationToken cancellationToken = default);
    Task<ICollection<Training>> GetByTrainerIdAsync(TrainerId trainerId, CancellationToken cancellationToken = default);
}
