using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;

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

    Task<Trainer?> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken = default);

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
    /// Gets all the <see cref="Trainer"/> entities from the repository.
    /// </summary>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> of <see cref="List{T}"/> of <see cref="Trainer"/> entities representing
    /// all the entities in the repository.
    /// </returns>
    Task<List<Trainer>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a <see cref="Trainer"/> entity for deletion.
    /// </summary>
    /// <param name="trainer">The <see cref="Trainer"/> entity to delete.</param>
    void Delete(Trainer trainer);
}
