using BLRefactoring.Shared.Common.Specifications;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.Specifications;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using BLRefactoring.Shared.Infrastructure.Specifications;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.Shared.Infrastructure.Repositories;
/// <summary>
/// Entity Framework Core implementation of <see cref="ITrainingRepository"/> and <see cref="IUniquenessTitleChecker"/>.
/// Provides data access for the Training aggregate using the Specification pattern.
/// </summary>
public class TrainingRepository(TrainingContext trainingContext) : ITrainingRepository, IUniquenessTitleChecker
{
    public async Task<Training?> GetByIdAsync(TrainingId id, CancellationToken cancellationToken = default) =>
        await trainingContext
            .Trainings
            .FirstOrDefaultAsync(training => training.Id == id, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Checks whether a training with the given title already exists for the specified trainer,
    /// using the <see cref="TrainingTitleExistsForTrainerSpecification"/>.
    /// </summary>
    public async Task<bool> TitleForTrainerExists(
        TrainingTitle title,
        TrainerId trainerId,
        CancellationToken cancellationToken = default)
    {
        var spec = new TrainingTitleExistsForTrainerSpecification(title, trainerId);
        return await AnyAsync(spec, cancellationToken);
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

    /// <summary>
    /// Retrieves all trainings belonging to the specified trainer,
    /// using the <see cref="TrainingsByTrainerSpecification"/>.
    /// </summary>
    public async Task<ICollection<Training>> GetByTrainerIdAsync(TrainerId trainerId, CancellationToken cancellationToken = default)
    {
        var spec = new TrainingsByTrainerSpecification(trainerId);
        return await GetAsync(spec, cancellationToken);
    }

    public Task<List<Training>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return trainingContext.Trainings.ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<Training>> GetAsync(ISpecification<Training> spec, CancellationToken cancellationToken = default)
    {
        return await SpecificationEvaluator.GetQuery(trainingContext.Trainings, spec)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Training?> FirstOrDefaultAsync(ISpecification<Training> spec, CancellationToken cancellationToken = default)
    {
        return await SpecificationEvaluator.GetQuery(trainingContext.Trainings, spec)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> AnyAsync(ISpecification<Training> spec, CancellationToken cancellationToken = default)
    {
        return await SpecificationEvaluator.GetQuery(trainingContext.Trainings, spec)
            .AnyAsync(cancellationToken);
    }
}
