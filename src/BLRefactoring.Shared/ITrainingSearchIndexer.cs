namespace BLRefactoring.Shared;

/// <summary>
/// Outbound port keeping a read-side search index in sync with the trainings,
/// consumed by application-layer code such as domain event handlers.
/// Implementations live in the infrastructure layer.
/// </summary>
/// <remarks>
/// The port speaks primitives, like every port of this shared kernel: the search
/// engine sitting behind it knows nothing about the domain's typed identifiers.
/// </remarks>
public interface ITrainingSearchIndexer
{
    /// <summary>
    /// Creates or refreshes the search index entry of the given training.
    /// </summary>
    /// <param name="trainingId">The identifier of the training to (re)index.</param>
    /// <param name="trainerId">The identifier of the trainer owning the training.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task IndexAsync(Guid trainingId, Guid trainerId, CancellationToken cancellationToken = default);
}
