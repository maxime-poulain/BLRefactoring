using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;

public sealed class TrainingId : EntityId<TrainingId>
{
    private TrainingId(Guid value) : base(value)
    {
    }
}
