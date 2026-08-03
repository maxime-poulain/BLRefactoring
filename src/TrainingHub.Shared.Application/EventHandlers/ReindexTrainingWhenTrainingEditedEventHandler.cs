using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;

namespace TrainingHub.Shared.Application.EventHandlers;

/// <summary>
/// Refreshes the read-side search index entry of an edited training.
/// </summary>
/// <remarks>
/// Counterpart of <see cref="IndexTrainingWhenTrainingCreatedEventHandler"/> for
/// edits: the indexer's upsert semantics make both handlers converge on the same
/// port, while each event keeps its own handler — the reactions to "created" and
/// "edited" can evolve independently (an edit might one day also invalidate a
/// cache or notify subscribed students).
/// </remarks>
/// <summary>
/// Reacts to the event: refreshes the search index for the edited training.
/// <para>
/// Dispatched inside the unit of work, before the transaction commits, so anything this handler
/// writes joins the same transaction as the change that raised the event.
/// </para>
/// </summary>
public sealed class ReindexTrainingWhenTrainingEditedEventHandler(ITrainingSearchIndexer searchIndexer)
    : IDomainEventHandler<TrainingEditedDomainEvent>
{
    /// <summary>
    /// Runs the reaction.
    /// </summary>
    /// <param name="notification">The event that was raised.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async ValueTask Handle(TrainingEditedDomainEvent notification, CancellationToken cancellationToken)
    {
        await searchIndexer.IndexAsync(
            notification.TrainingId.Value,
            notification.TrainerId.Value,
            cancellationToken);
    }
}
