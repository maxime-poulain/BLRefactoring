using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;

namespace BLRefactoring.Shared.Application.EventHandlers;

/// <summary>
/// Represents an event handler for the <see cref="TrainerDeletedDomainEvent"/>
/// that deletes all trainings of a given deleted trainer.
/// </summary>
public class DeleteTrainingWhenTrainerDeletedEventHandler(ITrainingRepository trainingRepository)
    : IDomainEventHandler<TrainerDeletedDomainEvent>
{
    public async ValueTask Handle(TrainerDeletedDomainEvent notification, CancellationToken cancellationToken)
    {
        // We could have also made a TrainingRepository.DeleteByTrainer(trainerId) method.
        // The staged deletions are persisted by the ambient SaveChanges that dispatched
        // this event — event handlers never commit through IUnitOfWork themselves.
        var trainings = await trainingRepository.GetByTrainerIdAsync(notification.TrainerId, cancellationToken);
        trainingRepository.Delete(trainings);
    }
}
