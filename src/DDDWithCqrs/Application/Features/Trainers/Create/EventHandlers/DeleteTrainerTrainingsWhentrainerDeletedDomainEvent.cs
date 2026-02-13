using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create.EventHandlers;

public class DeleteTrainerTrainingsWhenTrainerDeletedDomainEventHandler(ITrainingRepository trainingRepository)
    : IDomainEventHandler<TrainerDeletedDomainEvent>
{
    public async ValueTask Handle(TrainerDeletedDomainEvent notification, CancellationToken cancellationToken)
    {
        var trainings = await trainingRepository.GetByTrainerIdAsync(notification.Trainer.Id, cancellationToken).ConfigureAwait(false);
        await trainingRepository.DeleteAsync(trainings, cancellationToken);
    }
}
