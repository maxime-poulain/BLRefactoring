using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate.DomainEvents;

public class TrainerEmailChangedDomainEvent : IDomainEvent
{
    public TrainerEmailChangedDomainEvent(Trainer trainer)
    {
        Trainer = trainer;
    }

    public Trainer Trainer { get; }
}
