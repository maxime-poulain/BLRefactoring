using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using BLRefactoring.Shared.Infrastructure.ThirdParty.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BLRefactoring.Shared.Api.Extensions;

/// <summary>
/// Makes sure the schemas an API host depends on are up to date before it starts serving —
/// by applying the migrations in Development, and by checking in every other environment.
/// </summary>
public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// In Development, applies the pending migrations of the business and identity contexts, in
    /// that order. Everywhere else, applies nothing and reports whether any migration is pending.
    /// </summary>
    /// <remarks>
    /// Migrating on startup is a convenience of the development loop, not a deployment strategy:
    /// <c>docker compose up</c> followed by <c>dotnet run</c> gives a working database with no
    /// extra step, and the integration tests get their schema the same way — their host runs as
    /// Development for exactly that reason.
    /// <para>
    /// It is the wrong thing to do anywhere else. Two instances starting together migrate
    /// concurrently against the same database; the application needs permanent DDL rights on its
    /// own schema; and a deployment that alters the schema as a side effect of a process starting
    /// cannot be rolled back by stopping that process. Outside Development the schema is applied
    /// out of band — <c>dotnet ef database update</c>, or a migration bundle — as a deliberate,
    /// reversible step of the release.
    /// </para>
    /// <para>
    /// Skipping silently would swap one failure for another: a host that starts happily and then
    /// fails on the first request touching a missing column. So the pending migrations are still
    /// read and named in the log. They are reported rather than thrown, because a database that is
    /// briefly unreachable should not turn a rolling restart into an outage — the decision, and
    /// the fail-fast option that lost, are recorded in ADR 0003.
    /// </para>
    /// <para>
    /// Both hosts need the same treatment, so it lives here rather than in each <c>Program.cs</c>:
    /// a new context could otherwise be added to one and forgotten in the other. Each context is
    /// resolved in its own scope, since they are registered as scoped services and the
    /// application's root provider is not a scope.
    /// </para>
    /// </remarks>
    public static async Task EnsureDatabasesAreUpToDateAsync(
        this IHost app,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Emitting the OpenAPI document loads this host and stops it just before app.Run(), so
        // every statement above that line runs for real. Without this, generating an HTTP client
        // would migrate whatever database the configuration points at — a real one, on a
        // developer's machine. See OpenApiDocumentGeneration for which process is detected and why.
        if (OpenApiDocumentGeneration.IsInProgress())
        {
            return;
        }

        var environment = app.Services.GetRequiredService<IHostEnvironment>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(DatabaseMigrationExtensions).FullName!);

        if (environment.IsDevelopment())
        {
            await MigrateAsync<TrainingContext>(app, cancellationToken);
            await MigrateAsync<TrainingIdentityDbContext>(app, cancellationToken);
            return;
        }

        await ReportPendingMigrationsAsync<TrainingContext>(app, logger, cancellationToken);
        await ReportPendingMigrationsAsync<TrainingIdentityDbContext>(app, logger, cancellationToken);
    }

    private static async Task MigrateAsync<TContext>(IHost app, CancellationToken cancellationToken)
        where TContext : DbContext
    {
        using var scope = app.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }

    private static async Task ReportPendingMigrationsAsync<TContext>(
        IHost app,
        ILogger logger,
        CancellationToken cancellationToken)
        where TContext : DbContext
    {
        using var scope = app.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

        if (pending.Length == 0)
        {
            logger.LogInformation("{Context}: schema is up to date.", typeof(TContext).Name);
            return;
        }

        logger.LogCritical(
            "{Context}: {Count} migration(s) have not been applied to this database, and this host "
            + "does not apply them outside Development. Requests touching the affected tables will "
            + "fail. Pending: {Migrations}.",
            typeof(TContext).Name,
            pending.Length,
            string.Join(", ", pending));
    }
}
