using BLRefactoring.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore.Interceptors;

/// <summary>
/// Interceptor for handling domain events after saving changes in the DbContext.
/// This interceptor ensures that domain events are published automatically
/// without requiring explicit calls in the DbContext.
/// </summary>
public sealed class DomainEventInterceptor(IDomainEventPublisher publisher) : SaveChangesInterceptor
{
    /// <summary>
    /// Asynchronously handles the logic after changes are saved to the database.
    /// Publishes domain events for entities implementing <see cref="IHasDomainEvents"/>.
    /// </summary>
    /// <param name="eventData">The event data related to the save operation.</param>
    /// <param name="result">The result of the save operation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The result of the save operation.</returns>
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await PublishDomainEventsAsync(eventData.Context, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Handles the logic after changes are saved to the database.
    /// Publishes domain events for entities implementing <see cref="IHasDomainEvents"/>.
    /// </summary>
    /// <param name="eventData">The event data related to the save operation.</param>
    /// <param name="result">The result of the save operation.</param>
    /// <returns>The result of the save operation.</returns>
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is not null)
        {
            PublishDomainEventsAsync(eventData.Context, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }

        return base.SavedChanges(eventData, result);
    }

    /// <summary>
    /// Publishes domain events for all entities tracked by the DbContext
    /// that implement <see cref="IHasDomainEvents"/>.
    /// </summary>
    /// <param name="context">The DbContext instance.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task PublishDomainEventsAsync(DbContext context, CancellationToken cancellationToken)
    {
        // Retrieve all entities with domain events from the change tracker.
        var entitiesWithEvents = context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .ToArray();

        // Publish the domain events using the provided IEventPublisher.
        await publisher.PublishAsync(entitiesWithEvents, cancellationToken);
    }
}
