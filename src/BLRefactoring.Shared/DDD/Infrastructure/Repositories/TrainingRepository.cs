using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate.ValueObjects;
using BLRefactoring.Shared.DDD.Infrastructure.Repositories.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.Shared.DDD.Infrastructure.Repositories;
public class TrainingRepository(TrainingContext trainingContext)
    : ITrainingRepository, IUniquenessTitleChecker
{
    public Task<Training?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return trainingContext.Trainings
            .Include(training => training.Rates)
            .FirstOrDefaultAsync(training => training.Id == id, cancellationToken);
    }

    public async Task<bool> IsTitleUniqueAsync(string title, Trainer trainer, CancellationToken cancellationToken = default)
    {
        return !await trainingContext.Trainings
            .AnyAsync(training => training.Title == title &&
                                  training.TrainerId == trainer.Id, cancellationToken: cancellationToken);
    }

    public Task<IEnumerable<Training>> GetByTitleAsync(string title)
    {
        throw new NotImplementedException();
    }

    public async Task<IEnumerable<Training>> GetByTrainerAsync(Trainer trainer)
    {
        var trainings = await trainingContext.Trainings
            .Include(training => training.Rates)
            .Where(training => training.TrainerId == trainer.Id)
            .ToListAsync();

        return trainings;
    }

    public Task<IEnumerable<Training>> SearchByCriteriaAsync(TrainingSearchCriteria criteria)
    {
        throw new NotImplementedException();
    }

    public async Task SaveAsync(Training training, CancellationToken cancellationToken = default)
    {
        if (training.IsTransient())
        {
            await trainingContext.Trainings.AddAsync(training, cancellationToken);
        }
        else
        {
            trainingContext.Trainings.Update(training);
        }
        await trainingContext.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteAsync(IEnumerable<Training> trainings, CancellationToken cancellationToken)
    {
        trainingContext.Trainings.RemoveRange(trainings);
        return trainingContext.SaveChangesAsync(cancellationToken);
    }

    public Task<List<Training>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return trainingContext.Trainings
            .Include(training => training.Rates)
            .ToListAsync(cancellationToken);
    }
}
