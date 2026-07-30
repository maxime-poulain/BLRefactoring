using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Delete.EventHandlers;

public class DeleteTrainerTrainingsWhenTrainerDeletedEventHandler(ITrainingRepository trainingRepository)
    : IDomainEventHandler<TrainerDeletedDomainEvent>
{
    public async ValueTask Handle(TrainerDeletedDomainEvent notification, CancellationToken cancellationToken)
    {
        // The staged deletions are persisted by the ambient SaveChanges that dispatched
        // this event — event handlers never commit through IUnitOfWork themselves.
        var trainings = await trainingRepository.GetByTrainerIdAsync(notification.TrainerId, cancellationToken).ConfigureAwait(false);
        trainingRepository.Delete(trainings);
    }
}
