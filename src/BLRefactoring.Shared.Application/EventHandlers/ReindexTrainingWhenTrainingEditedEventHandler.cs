using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;

namespace BLRefactoring.Shared.Application.EventHandlers;

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
public sealed class ReindexTrainingWhenTrainingEditedEventHandler(ITrainingSearchIndexer searchIndexer)
    : IDomainEventHandler<TrainingEditedDomainEvent>
{
    public async ValueTask Handle(TrainingEditedDomainEvent notification, CancellationToken cancellationToken)
    {
        await searchIndexer.IndexAsync(
            notification.TrainingId.Value,
            notification.TrainerId.Value,
            cancellationToken);
    }
}
