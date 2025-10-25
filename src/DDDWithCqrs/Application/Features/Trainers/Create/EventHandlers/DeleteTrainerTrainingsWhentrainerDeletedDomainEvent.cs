using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;

namespace BLRefactoring.DDDWithCqrs.Application.Features.Trainers.Create.EventHandlers;

public class DeleteTrainerTrainingsWhenTrainerDeletedDomainEventHandler : IDomainEventHandler<TrainerDeletedDomainEvent>
{
    private readonly ITrainingRepository _trainingRepository;

    public DeleteTrainerTrainingsWhenTrainerDeletedDomainEventHandler(ITrainingRepository trainingRepository)
    {
        _trainingRepository = trainingRepository;
    }

    public async ValueTask Handle(TrainerDeletedDomainEvent notification, CancellationToken cancellationToken)
    {
        var trainings = await _trainingRepository.GetByTrainerAsync(notification.Trainer);
        await _trainingRepository.DeleteAsync(trainings, cancellationToken);
    }
}
