using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.DomainEvents;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate;

namespace TrainingHub.Shared.Application.EventHandlers;

/// <summary>
/// Reacts to <see cref="TrainerDeletedDomainEvent"/>: removes the trainings the departing trainer
/// owned, and has each of them announce its own disappearance.
/// </summary>
/// <remarks>
/// <para>
/// Dispatched inside the unit of work, before the transaction commits, so anything this handler
/// writes joins the same transaction as the change that raised the event.
/// </para>
/// <para>
/// The trainings announce themselves one by one, and that is not decoration. This is the third path
/// that deletes a training, and it was the one ADR 0050 missed: the other two call
/// <c>MarkForDeletion</c>, this one removed rows in bulk and said nothing, so the search index kept
/// serving every training a departing trainer had owned. It is also the path that does the most
/// damage when it is silent — one trainer leaving can orphan ten entries.
/// </para>
/// </remarks>
public sealed class DeleteTrainingWhenTrainerDeletedDomainEventHandler(ITrainingRepository trainingRepository)
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

        // Announced before being staged, and picked up without any further plumbing: the interceptor
        // drains domain events in rounds until none is left, and an entity marked Deleted stays in
        // the change tracker until the save completes — so these facts are dispatched on the next
        // round and committed by the very SaveChanges that is running.
        foreach (var training in trainings)
        {
            training.MarkForDeletion();
        }

        trainingRepository.Delete(trainings);
    }
}
