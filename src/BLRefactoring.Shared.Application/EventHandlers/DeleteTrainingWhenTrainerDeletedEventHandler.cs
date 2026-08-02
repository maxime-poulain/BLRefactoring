using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate;

namespace BLRefactoring.Shared.Application.EventHandlers;

/// <summary>
/// Represents an event handler for the <see cref="TrainerDeletedDomainEvent"/>
/// that deletes all trainings of a given deleted trainer.
/// </summary>
/// <summary>
/// Reacts to the event: removes the trainings the departing trainer owned.
/// <para>
/// Dispatched inside the unit of work, before the transaction commits, so anything this handler
/// writes joins the same transaction as the change that raised the event.
/// </para>
/// </summary>
public sealed class DeleteTrainingWhenTrainerDeletedEventHandler(ITrainingRepository trainingRepository)
    : IDomainEventHandler<TrainerDeletedDomainEvent>
{
    /// <summary>
    /// Runs the reaction.
    /// </summary>
    /// <param name="notification">The event that was raised.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async ValueTask Handle(TrainerDeletedDomainEvent notification, CancellationToken cancellationToken)
    {
        // We could have also made a TrainingRepository.DeleteByTrainer(trainerId) method.
        // The staged deletions are persisted by the ambient SaveChanges that dispatched
        // this event — event handlers never commit through IUnitOfWork themselves.
        var trainings = await trainingRepository.GetByTrainerIdAsync(notification.TrainerId, cancellationToken);
        trainingRepository.Delete(trainings);
    }
}
