using TrainingHub.Shared.Common.Pagination;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;

namespace TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;

/// <summary>
/// Represents a repository for the <see cref="Trainer"/> aggregate.
/// </summary>
/// <remarks>
/// Modification methods (<see cref="Add"/>, <see cref="Update"/>, <see cref="Delete"/>)
/// only stage changes in the underlying change tracker; nothing is persisted until the
/// orchestrating use case commits through the unit of work. Every read is a named method — the
/// generic specification-taking members the shared <c>IRepository</c> base used to impose are
/// gone with the base itself, for the reason recorded on <c>ITrainingRepository</c> and in
/// ADR 0028.
/// </remarks>
public interface ITrainerRepository
{
    /// <summary>
    /// Gets a <see cref="Trainer"/> entity with a specified <paramref name="id"/>.
    /// </summary>
    /// <param name="id">The ID of the <see cref="Trainer"/> entity to get.</param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> of <see cref="Trainer"/>? representing the retrieved entity,
    /// or null if no entity with the specified <paramref name="id"/> exists.
    /// </returns>
    Task<Trainer?> GetByIdAsync(TrainerId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the trainer behind an identity account.
    /// </summary>
    /// <param name="userId">The identity account.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The trainer, or <see langword="null"/> when there is none.</returns>
    Task<Trainer?> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tells whether a <see cref="Trainer"/> with the given <paramref name="id"/> exists.
    /// </summary>
    /// <remarks>
    /// Answers the only question a use case that merely guards on the trainer's existence
    /// actually asks. Loading the aggregate to test a null reference reads a name, a contact
    /// address and a bio for nothing, and tracks an entity nothing will change.
    /// </remarks>
    /// <param name="id">The identifier to look for.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task<bool> ExistsAsync(TrainerId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one page of trainers, newest first, optionally narrowed to a standing and to a term.
    /// </summary>
    /// <remarks>
    /// The first question here that carries criteria, and the one ADR 0055 exists to bound. The line
    /// it draws is <em>named criteria, never a predicate</em>: a status the domain declares and a
    /// term to look for, both of them parameters this method is free to ignore or reinterpret —
    /// never an expression, a specification or a queryable, which would hand a caller the shape of
    /// the query and make this a query surface rather than a question. ADR 0028 drew that line at
    /// zero criteria; this record moves it and says where it now is.
    /// <para>
    /// The order is not among them. <c>NewestFirst</c> is the only order any paged read here uses,
    /// which is what keeps the total order ADR 0001 requires and stops the two hosts paging
    /// differently.
    /// </para>
    /// <para>
    /// <paramref name="search"/> is matched against the trainer's first name, last name and contact
    /// address. What that costs — a <c>LIKE '%term%'</c> no index can seek — is recorded in ADR 0055
    /// rather than discovered, together with what replaces it when the catalogue outgrows it.
    /// </para>
    /// </remarks>
    /// <param name="status">The standing to narrow to, or <see langword="null"/> for every trainer.</param>
    /// <param name="search">The term to look for, or <see langword="null"/> or blank for no term.</param>
    /// <param name="paging">The page asked for.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The page, empty when nothing matches.</returns>
    Task<PagedResult<Trainer>> GetPageAsync(
        TrainerStatus? status,
        string? search,
        PageRequest paging,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a new <see cref="Trainer"/> entity for insertion.
    /// </summary>
    /// <param name="trainer">The <see cref="Trainer"/> entity to add.</param>
    void Add(Trainer trainer);

    /// <summary>
    /// Stages an existing <see cref="Trainer"/> entity for update.
    /// </summary>
    /// <param name="trainer">The <see cref="Trainer"/> entity to update.</param>
    void Update(Trainer trainer);

    /// <summary>
    /// Stages a <see cref="Trainer"/> entity for deletion.
    /// </summary>
    /// <param name="trainer">The <see cref="Trainer"/> entity to delete.</param>
    void Delete(Trainer trainer);
}
