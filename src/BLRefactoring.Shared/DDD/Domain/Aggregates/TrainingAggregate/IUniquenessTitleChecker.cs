using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate;

namespace BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;

public interface IUniquenessTitleChecker
{
    Task<bool> IsTitleUniqueAsync(
        string title,
        Trainer trainer,
        CancellationToken cancellationToken = default);
}
