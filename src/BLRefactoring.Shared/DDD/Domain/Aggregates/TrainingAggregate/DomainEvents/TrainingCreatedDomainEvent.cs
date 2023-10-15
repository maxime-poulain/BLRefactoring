using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate.DomainEvents;

internal class TrainingCreatedDomainEvent : IDomainEvent
{
    public Training Training { get; }

    public TrainingCreatedDomainEvent(Training training)
    {
        Training = training;
    }
}