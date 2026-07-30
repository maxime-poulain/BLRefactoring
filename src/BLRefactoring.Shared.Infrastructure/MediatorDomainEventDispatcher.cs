using BLRefactoring.Shared.Common;
using Mediator;

namespace BLRefactoring.Shared.Infrastructure;

/// <summary>
/// <see cref="IDomainEventDispatcher"/> implementation that dispatches each event of
/// the batch to its in-process handlers through the source-generated Mediator.
/// </summary>
/// <remarks>
/// Publishing one event at a time is an implementation detail of Mediator's
/// notification model; the architectural contract remains the batch. This class does
/// not touch the aggregates: collecting and clearing their events is the
/// responsibility of the caller.
/// </remarks>
public sealed class MediatorDomainEventDispatcher(IMediator mediator) : IDomainEventDispatcher
{
    /// <inheritdoc />
    public async Task DispatchAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            await mediator.Publish(domainEvent, cancellationToken);
        }
    }
}
