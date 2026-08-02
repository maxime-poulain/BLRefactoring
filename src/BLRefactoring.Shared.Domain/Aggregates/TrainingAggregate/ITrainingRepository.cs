using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;

/// <summary>
/// Represents a repository for the <see cref="Training"/> aggregate.
/// </summary>
/// <remarks>
/// Modification methods (<see cref="Add"/>, <see cref="Update"/>, <see cref="Delete(Training)"/>)
/// only stage changes in the underlying change tracker; nothing is persisted until the
/// orchestrating use case commits through the unit of work.
/// </remarks>
public interface ITrainingRepository : IRepository<Training>
{
    public Task<Training?> GetByIdAsync(TrainingId id, CancellationToken cancellationToken = default);

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

    Task<ICollection<Training>> GetByTrainerIdAsync(TrainerId trainerId, CancellationToken cancellationToken = default);
}
