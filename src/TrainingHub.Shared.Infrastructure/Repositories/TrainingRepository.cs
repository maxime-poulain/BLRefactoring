using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.Specifications;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using TrainingHub.Shared.Infrastructure.Pagination;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using Microsoft.EntityFrameworkCore;

namespace TrainingHub.Shared.Infrastructure.Repositories;

/// <summary>
/// Entity Framework Core implementation of <see cref="ITrainingRepository"/>,
/// <see cref="IUniquenessTitleChecker"/> and <see cref="ITrainingCounter"/>.
/// Provides data access for the Training aggregate using the Specification pattern.
/// </summary>
public sealed class TrainingRepository(TrainingContext trainingContext)
    : ITrainingRepository, IUniquenessTitleChecker, ITrainingCounter
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
    /// Counts the trainings that hold a place in the given trainer's quota — the data half of the
    /// catalogue-capacity rule, whose decision stays in <see cref="Training.CreateAsync"/>.
    /// </summary>
    /// <remarks>
    /// Two clauses that are not the same kind of thing, and are written apart for it: the
    /// <c>Where</c> scopes the rows to one trainer, which states no rule, while the count carries
    /// <see cref="TrainingCountsTowardTheQuotaSpecification"/>, which states one — only a training
    /// its owner withdrew frees a slot (ADR 0028, ADR 0050, ADR 0052).
    /// </remarks>
    public async Task<int> CountForTrainerAsync(
        TrainerId trainerId,
        CancellationToken cancellationToken = default)
    {
        var counted = new TrainingCountsTowardTheQuotaSpecification();

        return await trainingContext.Trainings
            .Where(training => training.TrainerId == trainerId)
            .CountAsync(counted.Criteria, cancellationToken)
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

    /// <summary>
    /// Reads one page of a trainer's trainings, newest first.
    /// </summary>
    /// <remarks>
    /// The same order every paged read uses — <c>NewestFirst</c> is the only way to obtain the
    /// <see cref="IOrderedQueryable{T}"/> the paging extension demands, so this read cannot page
    /// inconsistently with the CQRS host's. What differs is the bill: this one materialises the
    /// aggregates whole, topics included, because handing back aggregates is what a repository is
    /// for; the query handlers project the same page into columns. See ADR 0029.
    /// </remarks>
    public async Task<PagedResult<Training>> GetPageByTrainerIdAsync(
        TrainerId trainerId,
        PageRequest paging,
        CancellationToken cancellationToken = default)
    {
        return await trainingContext.Trainings
            .Where(training => training.TrainerId == trainerId)
            .NewestFirst<Training, TrainingId>()
            .ToPagedResultAsync(paging, cancellationToken)
            .ConfigureAwait(false);
    }
}
