using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate.DomainEvents;

public class TrainerCreatedDomainEvent : IDomainEvent
{
    public TrainerCreatedDomainEvent(Trainer trainer)
    {
        Trainer = trainer;
    }

    public Trainer Trainer { get; }
}
