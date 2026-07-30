using BLRefactoring.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore.Interceptors;

/// <summary>
/// Collects the domain events accumulated by the tracked aggregates and dispatches
/// them right before the changes are saved, so that everything — the original
/// changes and those made by the event handlers — is persisted in the same
/// <c>SaveChanges</c> call, within a single transaction.
/// </summary>
/// <remarks>
/// <para>
/// Events are drained in rounds: each round snapshots the pending events, clears
/// them from their aggregates, then dispatches the batch. Handlers may raise new
/// events on other aggregates; the loop repeats until no pending event remains.
/// </para>
/// <para>
/// Because dispatching happens before persistence, a failing handler aborts the
/// whole save: nothing is persisted. Domain event handlers must therefore never
/// call <see cref="IUnitOfWork"/> themselves — they participate in the ambient
/// save triggered by the orchestrating use case.
/// </para>
/// </remarks>
public sealed class DomainEventInterceptor(IDomainEventDispatcher dispatcher) : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await DispatchDomainEventsAsync(eventData.Context, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            DispatchDomainEventsAsync(eventData.Context, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }

        return base.SavingChanges(eventData, result);
    }

    /// <summary>
    /// Drains and dispatches the domain events of all tracked aggregates, in rounds,
    /// until no aggregate has pending events left.
    /// </summary>
    private async Task DispatchDomainEventsAsync(DbContext context, CancellationToken cancellationToken)
    {
        while (true)
        {
            var domainEvents = new List<IDomainEvent>();

            var entitiesWithEvents = context.ChangeTracker
                .Entries<IHasDomainEvents>()
                .Select(entry => entry.Entity)
                .Where(entity => entity.DomainEvents.Count > 0)
                .ToArray();

            foreach (var entity in entitiesWithEvents)
            {
                domainEvents.AddRange(entity.DomainEvents);
                entity.ClearDomainEvents();
            }

            if (domainEvents.Count == 0)
            {
                return;
            }

            await dispatcher.DispatchAsync(domainEvents, cancellationToken);
        }
    }
}
