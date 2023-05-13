using BLRefactoring.Shared.Common;

namespace BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate.DomainEvents;

public class TrainerDeletedDomainEvent : IDomainEvent
{
    public Trainer Trainer { get; }

    public TrainerDeletedDomainEvent(Trainer trainer)
    {
        this.Trainer = trainer;
    }
}