using BLRefactoring.Shared.Common;

namespace BLRefactoring.DDD.Domain.Aggregates.TrainingAggregate.ValueObjects;

public class TrainingSearchCriteria : ValueObject
{
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        return Array.Empty<object?>();
    }
}