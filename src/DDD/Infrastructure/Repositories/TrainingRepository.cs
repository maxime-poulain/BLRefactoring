using BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.DDD.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.DDD.Domain.Aggregates.TrainingAggregate.ValueObjects;
using BLRefactoring.DDD.Infrastructure.Repositories.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.DDD.Infrastructure.Repositories;
public class TrainingRepository : ITrainingRepository, IUniquenessTitleChecker
{
    private readonly TrainingContext _trainingContext;

    public TrainingRepository(TrainingContext trainingContext)
    {
        _trainingContext = trainingContext;
    }

    public Task<Training?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _trainingContext.Trainings
            .Include(training => training.Rates)
            .FirstOrDefaultAsync(training => training.Id == id, cancellationToken);
    }

    public async Task<bool> IsTitleUniqueAsync(string title, Trainer trainer, CancellationToken cancellationToken = default)
    {
        return true;
        return !await _trainingContext.Trainings
            .AnyAsync(training => training.Title == title &&
                                  training.TrainerIdd == trainer.Id, cancellationToken: cancellationToken);
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

    public async Task SaveAsync(Training training)
    {
        await _trainingContext.Trainings.AddAsync(training);
        await _trainingContext.SaveChangesAsync();
    }

    public Task<List<Training>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _trainingContext.Trainings
            .Include(training => training.Rates)
            .ToListAsync(cancellationToken);
    }
}
