using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;

/// <summary>
/// Represents a repository for the <see cref="Trainer"/> aggregate in a
/// Domain-Driven Design (DDD) architecture.
/// This interface inherits from a <see cref="IRepository{TEntity}"/> interface,
/// which is used to define a generic repository for <see cref="Trainer"/> entities.
/// </summary>
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
    /// A <see cref="ValueTask{TResult}"/> of <see cref="Trainer"/>? representing the retrieved entity,
    /// or null if no entity with the specified <paramref name="id"/> exists.
    /// </returns>
    ValueTask<Trainer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    ValueTask<Trainer?> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves changes to the <see cref="Trainer"/> entity.
    /// </summary>
    /// <param name="trainer">The <see cref="Trainer"/> entity to add or update.</param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous save operation.
    /// </returns>
    Task SaveAsync(Trainer trainer, CancellationToken cancellationToken = default);

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
    /// Deletes a <see cref="Trainer"/> entity from the repository.
    /// </summary>
    /// <param name="trainer">The <see cref="Trainer"/> entity to delete.</param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous delete operation.</returns>
    Task DeleteAsync(Trainer trainer, CancellationToken cancellationToken = default);
}
