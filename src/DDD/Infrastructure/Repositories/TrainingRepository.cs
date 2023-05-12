using BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.DDD.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.DDD.Domain.Aggregates.TrainingAggregate.ValueObjects;

namespace BLRefactoring.DDD.Infrastructure.Repositories;
public class TrainingRepository : ITrainingRepository, IUniquenessTitleChecker
{
    public Task<Training> GetByIdAsync(Guid id)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<Training>> GetByTitleAsync(string title)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<Training>> GetByTrainerAsync(Trainer trainer)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<Training>> SearchByCriteriaAsync(TrainingSearchCriteria criteria)
    {
        throw new NotImplementedException();
    }

    public Task<bool> IsTitleUnique(string title, Trainer trainer)
    {
        throw new NotImplementedException();
    }
}
