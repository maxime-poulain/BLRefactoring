using BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate;

namespace BLRefactoring.DDD.Domain.Aggregates.TrainingAggregate;

public interface IUniquenessTitleChecker
{
    Task<bool> IsTitleUnique(string title, Trainer trainer);
}
