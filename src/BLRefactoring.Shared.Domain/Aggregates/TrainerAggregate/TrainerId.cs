using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate;

public sealed class TrainerId : EntityId<TrainerId>
{
    private TrainerId(Guid value) : base(value)
    {
    }
}
