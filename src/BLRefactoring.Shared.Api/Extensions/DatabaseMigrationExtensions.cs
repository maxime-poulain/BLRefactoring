using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using BLRefactoring.Shared.Infrastructure.ThirdParty.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BLRefactoring.Shared.Api.Extensions;

/// <summary>
/// Brings the schemas an API host depends on up to date before it starts serving.
/// </summary>
public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Applies the pending migrations of the business and identity contexts, in that order.
    /// </summary>
    /// <remarks>
    /// Both hosts migrate the same two contexts, so leaving this in each <c>Program.cs</c> meant
    /// a new context could be added to one and forgotten in the other — a host that then starts
    /// fine and fails on the first request that touches the missing table.
    /// <para>
    /// Each context is resolved in its own scope, as it was: they are registered as scoped
    /// services, and the application's root provider is not a scope.
    /// </para>
    /// </remarks>
    public static async Task MigrateDatabasesAsync(this IHost app, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);

        await MigrateAsync<TrainingContext>(app, cancellationToken);
        await MigrateAsync<TrainingIdentityDbContext>(app, cancellationToken);
    }

    private static async Task MigrateAsync<TContext>(IHost app, CancellationToken cancellationToken)
        where TContext : DbContext
    {
        using var scope = app.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
