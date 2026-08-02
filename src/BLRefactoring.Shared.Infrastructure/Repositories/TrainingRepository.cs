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
public sealed class TrainingRepository(TrainingContext trainingContext) : ITrainingRepository, IUniquenessTitleChecker
{
    public async Task<Training?> GetByIdAsync(TrainingId id, CancellationToken cancellationToken = default) =>
        await trainingContext
            .Trainings
            .FirstOrDefaultAsync(training => training.Id == id, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Checks whether a training with the given title already exists for the specified trainer,
    /// using the <see cref="TrainingTitleExistsForTrainerSpecification"/>.
    /// </summary>
    public async Task<bool> TitleForTrainerExistsAsync(
        TrainingTitle title,
        TrainerId trainerId,
        CancellationToken cancellationToken = default)
    {
        var spec = new TrainingTitleExistsForTrainerSpecification(title, trainerId);
        return await AnyAsync(spec, cancellationToken);
    }

    public void Add(Training training)
    {
        trainingContext.Trainings.Add(training);
    }

    // Marking the aggregate modified is what makes optimistic concurrency work here,
    // not just a formality on an already-tracked instance: it guarantees an UPDATE on
    // the Training row, and therefore a check of its concurrency token, even when the
    // edition only touched owned rows in another table such as TrainingTopic.
    public void Update(Training training)
    {
        trainingContext.Trainings.Update(training);
    }

    public void Delete(Training training)
    {
        trainingContext.Trainings.Remove(training);
    }

    public void Delete(IEnumerable<Training> trainings)
    {
        trainingContext.Trainings.RemoveRange(trainings);
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

    /// <inheritdoc />
    public async Task<List<Training>> GetAsync(ISpecification<Training> spec, CancellationToken cancellationToken = default)
    {
        return await SpecificationEvaluator.GetQuery(trainingContext.Trainings, spec)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> AnyAsync(ISpecification<Training> spec, CancellationToken cancellationToken = default)
    {
        return await SpecificationEvaluator.GetQuery(trainingContext.Trainings, spec)
            .AnyAsync(cancellationToken);
    }
}
