using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.Specifications;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace TrainingHub.Shared.Infrastructure.Repositories;

/// <summary>
/// Entity Framework Core implementation of <see cref="ITrainingRepository"/> and <see cref="IUniquenessTitleChecker"/>.
/// Provides data access for the Training aggregate using the Specification pattern.
/// </summary>
public sealed class TrainingRepository(TrainingContext trainingContext) : ITrainingRepository, IUniquenessTitleChecker
{
    /// <summary>
    /// Finds a training by identifier, or <see langword="null"/> when there is none.
    /// </summary>
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
        // The specification travels as its Criteria: the repository is where a rule's expression
        // meets the database, and no evaluator is needed to hand a predicate to a Where.
        var spec = new TrainingTitleExistsForTrainerSpecification(title, trainerId);

        return await trainingContext.Trainings
            .AnyAsync(spec.Criteria, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Registers a new training to be written when the unit of work commits.
    /// </summary>
    public void Add(Training training)
    {
        trainingContext.Trainings.Add(training);
    }

    // Marking the aggregate modified is what makes optimistic concurrency work here,
    // not just a formality on an already-tracked instance: it guarantees an UPDATE on
    // the Training row, and therefore a check of its concurrency token, even when the
    // edition only touched owned rows in another table such as TrainingTopic.

    /// <summary>
    /// Marks a training as changed.
    /// </summary>
    public void Update(Training training)
    {
        trainingContext.Trainings.Update(training);
    }

    /// <summary>
    /// Registers a training for removal.
    /// </summary>
    public void Delete(Training training)
    {
        trainingContext.Trainings.Remove(training);
    }

    /// <summary>
    /// Registers several trainings for removal in one go.
    /// </summary>
    public void Delete(IEnumerable<Training> trainings)
    {
        trainingContext.Trainings.RemoveRange(trainings);
    }

    /// <summary>
    /// Retrieves all trainings belonging to the specified trainer.
    /// </summary>
    /// <remarks>
    /// A plain <c>Where</c>, and it used to be a specification. "The trainings of X" states no
    /// rule — it is data scoping, and dressing it as a domain concept was the first step of the
    /// drift ADR 0028 closes: a specification names a business rule, or it does not exist.
    /// </remarks>
    public async Task<ICollection<Training>> GetByTrainerIdAsync(TrainerId trainerId, CancellationToken cancellationToken = default)
    {
        return await trainingContext.Trainings
            .Where(training => training.TrainerId == trainerId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

}
