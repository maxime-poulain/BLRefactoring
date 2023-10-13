using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate.DomainEvents;

public class TrainerNameChangedDomainEvent : IDomainEvent
{
    public TrainerNameChangedDomainEvent(Trainer trainer)
    {
        Trainer = trainer;
    }

    public Trainer Trainer { get; }
}
