using BLRefactoring.Shared.Common;
using Mediator;

namespace BLRefactoring.Shared.Infrastructure;

public class MediatorRDomainEventPublisher(IMediator mediator) : IEventPublisher
{
    /// <inheritdoc />
    public async Task PublishAsync(IHasDomainEvents[] havingDomainEvents, CancellationToken cancellationToken)
    {
        // Domain Events can have an OccurredOn property.
        // This way we can order events by the time they occurred.

        foreach (var hasDomainEvents in havingDomainEvents)
        {
            foreach (var domainEvent in hasDomainEvents.DomainEvents)
            {
                await mediator.Publish(domainEvent, cancellationToken);
            }

            hasDomainEvents.ClearDomainEvents();
        }
    }
}
