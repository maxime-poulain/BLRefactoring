using BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.DDD.Domain.Aggregates.TrainingAggregate.ValueObjects;
using BLRefactoring.Shared.Common;

namespace BLRefactoring.DDD.Domain.Aggregates.TrainingAggregate;

public interface ITrainingRepository : IRepository<Training>
{
    public Task<Training?> GetByIdAsync(Guid id);

    public Task<IEnumerable<Training>> GetByTitleAsync(string title);

    public Task<IEnumerable<Training>> GetByTrainerAsync(Trainer trainer);

    public Task<IEnumerable<Training>> SearchByCriteriaAsync(TrainingSearchCriteria criteria);
}
