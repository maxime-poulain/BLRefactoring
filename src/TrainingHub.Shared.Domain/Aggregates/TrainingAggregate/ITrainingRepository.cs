using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;

namespace TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;

/// <summary>
/// Represents a repository for the <see cref="Training"/> aggregate.
/// </summary>
/// <remarks>
/// Modification methods (<see cref="Add"/>, <see cref="Update"/>, <see cref="Delete(Training)"/>)
/// only stage changes in the underlying change tracker; nothing is persisted until the
/// orchestrating use case commits through the unit of work.
/// <para>
/// Every read is a named method, deliberately: the repository used to inherit generic
/// <c>GetAsync(ISpecification)</c> members, which let any caller compose arbitrary criteria — a
/// query DSL wearing a domain word. A repository's surface is the list of questions the use cases
/// actually ask; specifications state rules, and are consumed by the implementation, not passed
/// through it. See ADR 0028.
/// </para>
/// </remarks>
public interface ITrainingRepository
{
    /// <summary>
    /// Get by id this i training repository.
    /// </summary>
    Task<Training?> GetByIdAsync(TrainingId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a new <see cref="Training"/> entity for insertion.
    /// </summary>
    void Add(Training training);

    /// <summary>
    /// Stages an existing <see cref="Training"/> entity for update.
    /// </summary>
    void Update(Training training);

    /// <summary>
    /// Stages a <see cref="Training"/> entity for deletion.
    /// </summary>
    void Delete(Training training);

    /// <summary>
    /// Stages a collection of <see cref="Training"/> entities for deletion.
    /// </summary>
    void Delete(IEnumerable<Training> trainings);

    /// <summary>
    /// Reads every training a trainer owns.
    /// </summary>
    /// <remarks>
    /// Unbounded on purpose, for the one caller that genuinely needs everything: the deletion
    /// cascade, which must remove each of them. A screen never calls this — a screen asks
    /// <see cref="GetPageByTrainerIdAsync"/>.
    /// </remarks>
    /// <param name="trainerId">The owning trainer.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The trainings, empty when there are none.</returns>
    Task<ICollection<Training>> GetByTrainerIdAsync(TrainerId trainerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one page of a trainer's trainings, newest first.
    /// </summary>
    /// <remarks>
    /// Still a named question, not a query surface: the order is fixed and the caller chooses
    /// nothing but the page coordinates — no criteria travels through here, which is the line
    /// ADR 0028 draws. The page itself is kernel vocabulary, like <c>Result</c>, so taking one
    /// does not teach the domain anything about screens; what it says is that no use case may
    /// read this collection unbounded, which is a server policy rather than a presentation
    /// concern. See ADR 0029.
    /// </remarks>
    /// <param name="trainerId">The owning trainer.</param>
    /// <param name="paging">The page asked for.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The page, empty when the trainer has nothing.</returns>
    Task<PagedResult<Training>> GetPageByTrainerIdAsync(
        TrainerId trainerId,
        PageRequest paging,
        CancellationToken cancellationToken = default);
}
