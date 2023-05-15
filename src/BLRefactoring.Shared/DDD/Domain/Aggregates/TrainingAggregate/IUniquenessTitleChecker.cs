using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate;

namespace BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;

/// <summary>
/// Provides a method to check the uniqueness of a title in association with a <see cref="Trainer"/>.
/// </summary>
public interface IUniquenessTitleChecker
{
    /// <summary>
    /// Determines whether the specified title is unique for the given <see cref="Trainer"/> asynchronously.
    /// </summary>
    /// <param name="title">The title to check for uniqueness.</param>
    /// <param name="trainer">The associated <see cref="Trainer"/> for which the title's uniqueness is to be checked.</param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains <see langword="true"/>
    /// if the title is unique for the specified trainer; otherwise, <see langword="false"/>.
    /// </returns>
    Task<bool> IsTitleUniqueAsync(
        string title,
        Trainer trainer,
        CancellationToken cancellationToken = default);
}
