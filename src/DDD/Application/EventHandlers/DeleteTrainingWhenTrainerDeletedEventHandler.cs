using BLRefactoring.DDD.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.DDD.Domain.Aggregates.TrainingAggregate;
using BLRefactoring.Shared.Common;

namespace BLRefactoring.DDD.Application.EventHandlers;

public class DeleteTrainingWhenTrainerDeletedEventHandler : IDomainEventHandler<TrainerDeletedDomainEvent>
{
    private readonly ITrainingRepository _trainingRepository;

    public DeleteTrainingWhenTrainerDeletedEventHandler(ITrainingRepository trainingRepository)
    {
        _trainingRepository = trainingRepository;
    }

    public async Task Handle(TrainerDeletedDomainEvent notification, CancellationToken cancellationToken)
    {
        // We could have also made a TrainingRepository.DeleteByTrainer(trainerId) method.
        var trainings = await _trainingRepository.GetByTrainerAsync(notification.Trainer);
        await _trainingRepository.DeleteAsync(trainings, cancellationToken);
    }
}
