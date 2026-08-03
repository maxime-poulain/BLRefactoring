using TrainingHub.Shared.Common;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;

namespace TrainingHub.Shared.Application.EventHandlers;

/// <summary>
/// Adds a freshly created training to the read-side search index.
/// </summary>
/// <remarks>
/// Illustrates read-model synchronization as a use of a domain event: the write
/// model announces the fact, and a handler keeps a derived model (here a search
/// index) up to date without the aggregate knowing that such a model exists.
/// </remarks>
/// <summary>
/// Reacts to the event: adds the new training to the search index.
/// <para>
/// Dispatched inside the unit of work, before the transaction commits, so anything this handler
/// writes joins the same transaction as the change that raised the event.
/// </para>
/// </summary>
public sealed class IndexTrainingWhenTrainingCreatedEventHandler(ITrainingSearchIndexer searchIndexer)
    : IDomainEventHandler<TrainingCreatedDomainEvent>
{
    /// <summary>
    /// Runs the reaction.
    /// </summary>
    /// <param name="notification">The event that was raised.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async ValueTask Handle(TrainingCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        await searchIndexer.IndexAsync(
            notification.TrainingId.Value,
            notification.TrainerId.Value,
            cancellationToken);
    }
}
