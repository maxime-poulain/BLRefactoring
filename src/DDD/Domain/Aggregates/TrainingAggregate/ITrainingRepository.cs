using BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.DDD.Domain.Aggregates.TrainingAggregate.ValueObjects;
using BLRefactoring.Shared.Common;

namespace BLRefactoring.DDD.Domain.Aggregates.TrainingAggregate;

public interface ITrainingRepository : IRepository<Training>
{
    public Task<Training?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    public Task<IEnumerable<Training>> GetByTitleAsync(string title);

    public Task<IEnumerable<Training>> GetByTrainerAsync(Trainer trainer);

    public Task<IEnumerable<Training>> SearchByCriteriaAsync(TrainingSearchCriteria criteria);
    Task SaveAsync(Training training);
    Task<List<Training>> GetAllAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(IEnumerable<Training> trainings, CancellationToken cancellationToken = default);
}
