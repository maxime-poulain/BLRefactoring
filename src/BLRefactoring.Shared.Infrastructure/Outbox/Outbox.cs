using BLRefactoring.Shared;
using BLRefactoring.Shared.Common;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;

namespace BLRefactoring.Shared.Infrastructure.Outbox;

/// <summary>
/// Stages integration events as rows of the business database.
/// </summary>
/// <remarks>
/// <see cref="Enqueue"/> only adds to the change tracker; the row is written by whichever
/// <c>SaveChanges</c> is already in flight. That is what makes the guarantee hold — the message
/// and the change it announces are one write, so a rolled-back transaction takes the
/// announcement with it.
/// <para>
/// Callers are the domain event handlers, which run inside <c>SavingChangesAsync</c> from
/// <c>DomainEventInterceptor</c>. Adding an entity at that point is still in time to be
/// persisted by the same call.
/// </para>
/// </remarks>
public sealed class Outbox(TrainingContext context) : IOutbox
{
    public void Enqueue(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        context.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Type = integrationEvent.GetType().AssemblyQualifiedName!,
            Payload = OutboxSerializer.Serialize(integrationEvent),
            OccurredOn = integrationEvent.OccurredOn
        });
    }
}
