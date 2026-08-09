using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;

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

    /// <summary>
    /// Reads one page of trainings across every trainer, newest first, optionally narrowed to a
    /// state and to a term.
    /// </summary>
    /// <remarks>
    /// The sibling above is scoped to one trainer and this one is not, which is the whole difference
    /// and the reason it needed a record before it could exist: reading across every trainer is what
    /// this API withdrew when it deleted <c>/Training/all</c>, and what ADR 0055 gives back to one
    /// role rather than to every authenticated caller.
    /// <para>
    /// Criteria named one by one, never a predicate — the line ADR 0055 draws and the rule that
    /// defends it. The order stays <c>NewestFirst</c> for the reason every paged read here shares:
    /// it is the total order ADR 0001 requires, and the caller does not choose it.
    /// </para>
    /// <para>
    /// <c>search</c> is matched against the title and against nothing else: a description and a set
    /// of prerequisites are prose, and searching them turns a bounded scan into an unbounded one for
    /// matches nobody asked for — the argument ADR 0055 makes for keeping a bio out of the trainers'
    /// term. This question carried no term at all until ADR 0060, for a reason that was never a
    /// choice: a title mapped through a value converter is one EF Core cannot look inside, and
    /// remapping it as a complex property is what made a substring match translate.
    /// </para>
    /// <para>
    /// What the term costs — a <c>LIKE '%term%'</c> no index can seek — is recorded in ADR 0055 for
    /// the trainers and is unchanged here. The search that seeks is the public catalogue's, over an
    /// index of its own (ADR 0059), and it cannot answer this one: it holds none of the states a
    /// moderator is looking for.
    /// </para>
    /// </remarks>
    /// <param name="status">The state to narrow to, or <see langword="null"/> for every training.</param>
    /// <param name="search">The term to look for, or <see langword="null"/> or blank for no term.</param>
    /// <param name="paging">The page asked for.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The page, empty when nothing matches.</returns>
    Task<PagedResult<Training>> GetPageAsync(
        TrainingStatus? status,
        string? search,
        PageRequest paging,
        CancellationToken cancellationToken = default);
}
