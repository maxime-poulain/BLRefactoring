using BLRefactoring.Shared.Common;

namespace BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate.DomainEvents;

public class TrainerCreatedDomainEvent(Trainer trainer) : IDomainEvent
{
    public Trainer Trainer { get; } = trainer;
}
