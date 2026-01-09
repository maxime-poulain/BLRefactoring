using BLRefactoring.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore.Interceptors;


/// <summary>
/// Interceptor that automatically sets CreatedOn and ModifiedOn properties for auditable entities.
/// </summary>
public sealed class AuditableEntitiesInterceptor : SaveChangesInterceptor
{
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

    private static void UpdateAuditableEntities(DbContext context)
    {
        var now = DateTime.UtcNow;
        var entries = context.ChangeTracker.Entries<IAuditable>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(IAuditable.CreatedOn)).CurrentValue = now;
                entry.Property(nameof(IAuditable.ModifiedOn)).IsModified = false;

            }

            if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(IAuditable.ModifiedOn)).CurrentValue = now;
                entry.Property(nameof(IAuditable.CreatedOn)).IsModified = false;
            }
        }
    }
}
