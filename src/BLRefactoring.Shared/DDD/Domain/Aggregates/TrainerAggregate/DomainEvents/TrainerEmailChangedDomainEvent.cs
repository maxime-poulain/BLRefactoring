using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate.DomainEvents;
internal class TrainerEmailChangedDomainEvent : IDomainEvent
{
    public TrainerEmailChangedDomainEvent(Trainer trainer)
    {
        Trainer = trainer;
    }

    public Trainer Trainer { get; }
}
