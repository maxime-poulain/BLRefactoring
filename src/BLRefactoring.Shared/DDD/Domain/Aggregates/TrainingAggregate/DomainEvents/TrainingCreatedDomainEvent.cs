using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate.DomainEvents;

public class TrainingCreatedDomainEvent(Training training) : IDomainEvent
{
    public Training Training { get; } = training;
}
