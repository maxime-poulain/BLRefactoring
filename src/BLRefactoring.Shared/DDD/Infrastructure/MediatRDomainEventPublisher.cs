using BLRefactoring.Shared.Common;
using Mediator;

namespace BLRefactoring.Shared.DDD.Infrastructure;

public class MediatRDomainEventPublisher : IEventPublisher
{
    private readonly IMediator _mediator;

    public MediatRDomainEventPublisher(IMediator mediator) => _mediator = mediator;

    /// <inheritdoc />
    public async Task PublishAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        // Domain Events can have an OccurredOn property.
        // This way we ca order events by the time they occurred.

        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
        }
    }
}
