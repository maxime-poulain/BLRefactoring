using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.Shared.Infrastructure.Repositories;
public class TrainingRepository(TrainingContext trainingContext) : ITrainingRepository, IUniquenessTitleChecker
{
    public async Task<Training?> GetByIdAsync(TrainingId id, CancellationToken cancellationToken = default) =>
        await trainingContext
            .Trainings
            .FirstOrDefaultAsync(training => training.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task<bool> TitleForTrainerExists(
        TrainingTitle title,
        TrainerId trainerId,
        CancellationToken cancellationToken = default)
    {
        return await trainingContext.
            Trainings
            .AnyAsync(training =>
                training.Title == title &&
                training.TrainerId == trainerId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    // GetByTrainingIdAsync
    public async Task<Training?> GetByTrainerIdAsync(TrainingId trainingId, CancellationToken cancellationToken = default)
    {
        return await trainingContext.Trainings
            .FirstOrDefaultAsync(training => training.Id == trainingId, cancellationToken);
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

    public async Task DeleteAsync(Training training, CancellationToken cancellationToken = default)
    {
        trainingContext.Trainings.Remove(training);
        await trainingContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAsync(IEnumerable<Training> trainings, CancellationToken cancellationToken = default)
    {
        trainingContext.Trainings.RemoveRange(trainings);
        return trainingContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ICollection<Training>> GetByTrainerIdAsync(TrainerId trainerId, CancellationToken cancellationToken = default)
    {
        return await trainingContext.Trainings
            .Where(training => training.TrainerId == trainerId)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Training>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return trainingContext.Trainings.ToListAsync(cancellationToken);
    }
}
