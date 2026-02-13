using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;

public class TrainingCreatedDomainEvent(Training training) : IDomainEvent
{
    public Training Training { get; } = training;
}

public class TrainingEditedDomainEvent(Training training) : IDomainEvent
{
    public Training Training { get; } = training;
}
