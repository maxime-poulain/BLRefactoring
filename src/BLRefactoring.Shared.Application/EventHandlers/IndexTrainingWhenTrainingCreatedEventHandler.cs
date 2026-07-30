using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.DomainEvents;

namespace BLRefactoring.Shared.Application.EventHandlers;

/// <summary>
/// Adds a freshly created training to the read-side search index.
/// </summary>
/// <remarks>
/// Illustrates read-model synchronization as a use of a domain event: the write
/// model announces the fact, and a handler keeps a derived model (here a search
/// index) up to date without the aggregate knowing that such a model exists.
/// </remarks>
public sealed class IndexTrainingWhenTrainingCreatedEventHandler(ITrainingSearchIndexer searchIndexer)
    : IDomainEventHandler<TrainingCreatedDomainEvent>
{
    public async ValueTask Handle(TrainingCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        await searchIndexer.IndexAsync(
            notification.TrainingId.Value,
            notification.TrainerId.Value,
            cancellationToken);
    }
}
