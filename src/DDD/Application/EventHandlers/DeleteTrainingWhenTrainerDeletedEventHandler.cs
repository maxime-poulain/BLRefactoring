using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.DDD.Domain.Aggregates.TrainingAggregate;

namespace BLRefactoring.DDD.Application.EventHandlers;

/// <summary>
/// Represents an event handler for the <see cref="TrainerDeletedDomainEvent"/>
/// that deletes all trainings of a given deleted trainer.
/// </summary>
public class DeleteTrainingWhenTrainerDeletedEventHandler : IDomainEventHandler<TrainerDeletedDomainEvent>
{
    private readonly ITrainingRepository _trainingRepository;

    public DeleteTrainingWhenTrainerDeletedEventHandler(ITrainingRepository trainingRepository)
        => _trainingRepository = trainingRepository;

    public async Task Handle(TrainerDeletedDomainEvent notification, CancellationToken cancellationToken)
    {
        // We could have also have made a TrainingRepository.DeleteByTrainer(trainerId) method.
        var trainings = await _trainingRepository.GetByTrainerAsync(notification.Trainer);
        await _trainingRepository.DeleteAsync(trainings, cancellationToken);
    }
}
