using TrainingHub.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Interceptors;

/// <summary>
/// Stamps <see cref="IAuditable.CreatedOn"/> and <see cref="IAuditable.ModifiedOn"/> as entities
/// are saved.
/// </summary>
/// <param name="timeProvider">
/// The clock the stamps are read from. Injected rather than reached for statically, which is what
/// makes the stamping testable: a test supplies a clock it controls and asserts what was written,
/// where <c>DateTime.UtcNow</c> left nothing to assert against.
/// </param>
public sealed class AuditableEntitiesInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    /// <summary>
    /// Stamps CreatedOn and ModifiedOn before an asynchronous save.
    /// <para>
    /// Here rather than in the domain, so an aggregate never needs to be handed a clock.
    /// </para>
    /// </summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            UpdateAuditableEntities(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Stamps CreatedOn and ModifiedOn before a synchronous save.
    /// </summary>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            UpdateAuditableEntities(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    /// <remarks>
    /// The clock is read once per entity, inside the loop, so each carries the instant it was
    /// stamped at rather than one reading shared by the batch.
    /// <para>
    /// It does not make <c>CreatedOn</c> a unique value, and nothing here should be taken to say
    /// otherwise. <see cref="TimeProvider.GetUtcNow"/> reads the system clock, which advances at
    /// the platform's timer interval — around 15.6 ms on Windows by default — so successive calls
    /// within one tick return the same instant, and two concurrent saves collide just as easily.
    /// Anything needing a total order over rows must still break ties on the identifier.
    /// </para>
    /// </remarks>
    private void UpdateAuditableEntities(DbContext context)
    {
        var entries = context.ChangeTracker.Entries<IAuditable>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(IAuditable.CreatedOn)).CurrentValue = timeProvider.GetUtcNow().UtcDateTime;
                entry.Property(nameof(IAuditable.ModifiedOn)).IsModified = false;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(IAuditable.ModifiedOn)).CurrentValue = timeProvider.GetUtcNow().UtcDateTime;
                entry.Property(nameof(IAuditable.CreatedOn)).IsModified = false;
            }
        }
    }
}
