using TrainingHub.Shared.Common;

namespace TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;

/// <summary>
/// Represents a repository for the <see cref="Trainer"/> aggregate in a
/// Domain-Driven Design (DDD) architecture.
/// This interface inherits from a <see cref="IRepository{TEntity}"/> interface,
/// which is used to define a generic repository for <see cref="Trainer"/> entities.
/// </summary>
/// <remarks>
/// Modification methods (<see cref="Add"/>, <see cref="Update"/>, <see cref="Delete"/>)
/// only stage changes in the underlying change tracker; nothing is persisted until the
/// orchestrating use case commits through the unit of work.
/// </remarks>
public interface ITrainerRepository : IRepository<Trainer>
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
