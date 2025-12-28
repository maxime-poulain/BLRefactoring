using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create.EventHandlers;

public class DeleteTrainerTrainingsWhenTrainerDeletedDomainEventHandler(
    ITrainingRepository trainingRepository) : IDomainEventHandler<TrainerDeletedDomainEvent>
{
    public async ValueTask Handle(TrainerDeletedDomainEvent notification, CancellationToken cancellationToken)
    {
        var trainings = await trainingRepository.GetByTrainerAsync(notification.Trainer);
        await trainingRepository.DeleteAsync(trainings, cancellationToken);
    }
}
