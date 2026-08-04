using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate;

namespace TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;

/// <summary>
/// Answers how many trainings a trainer already publishes.
/// </summary>
/// <remarks>
/// The data half of the catalogue-capacity rule, the same shape as
/// <see cref="IUniquenessTitleChecker"/> for title uniqueness: the aggregate owns the decision —
/// no trainer publishes more than <see cref="Training.MaximumPerTrainer"/> trainings — but the
/// fact it decides on lives in rows it cannot see, so the question comes to the factory through
/// this port. The port answers the raw count rather than "is the catalogue full", on purpose:
/// an implementation that answered the decision would own half of it. See ADR 0030.
/// </remarks>
public interface ITrainingCounter
{
    /// <summary>
    /// Counts the trainings the given trainer currently publishes.
    /// </summary>
    /// <param name="trainerId">The trainer whose catalogue is being measured.</param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>How many trainings that trainer publishes today.</returns>
    Task<int> CountForTrainerAsync(TrainerId trainerId, CancellationToken cancellationToken = default);
}
