using System.Data.Common;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore.Interceptors;
using BLRefactoring.Shared.Infrastructure.Outbox;
using BLRefactoring.Shared.Infrastructure.ThirdParty.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Respawn;
using Respawn.Graph;
using Testcontainers.MsSql;
using Xunit;

namespace BLRefactoring.Api.TestKit;

/// <summary>
/// Hosts an API against a real SQL Server started by Testcontainers, and empties it between
/// tests. Generic over the entry point so both stacks share one implementation: the two
/// <see cref="DbContext"/> it replaces live in the shared infrastructure and are common to
/// both hosts, leaving <typeparamref name="TEntryPoint"/> as the only difference.
/// </summary>
public abstract class ApiFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>, IAsyncLifetime, IResettableDatabase
    where TEntryPoint : class
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private Respawner _respawner = null!;

    // Respawn works against an open DbConnection; one is kept for the lifetime of
    // the fixture rather than reopened per reset.
    private DbConnection _connection = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // The interceptors are re-attached deliberately. Replacing the options
            // drops the ones AddInfrastructure had configured, and without them no
            // domain event is dispatched and the audit columns stay unstamped —
            // the suite would exercise a different pipeline than production and
            // pass for the wrong reasons.
            ReplaceDbContext<TrainingContext>(services, (serviceProvider, options) =>
                options.AddInterceptors(
                    serviceProvider.GetRequiredService<DomainEventInterceptor>(),
                    serviceProvider.GetRequiredService<AuditableEntitiesInterceptor>()));

            ReplaceDbContext<TrainingIdentityDbContext>(services);

            StopTheOutboxFromDrainingItself(services);
        });

        builder.UseEnvironment("Development");
    }

    /// <summary>
    /// Removes the outbox's background service, so the queue only moves when a test says so.
    /// </summary>
    /// <remarks>
    /// Left registered, the host starts it: it drains immediately and then every ten seconds,
    /// across a suite that runs for the best part of a minute. A tick landing between a request
    /// and its assertion turns "this message is still pending" into a failure — intermittently,
    /// which is worse than always, because a green run would prove nothing.
    /// <para>
    /// Removing it does not weaken the suite. The tests already drive
    /// <c>OutboxProcessor.ProcessPendingAsync</c> themselves, which is what makes "draining twice
    /// does not republish" a real assertion rather than a race against a concurrent tick.
    /// </para>
    /// </remarks>
    private static void StopTheOutboxFromDrainingItself(IServiceCollection services)
    {
        foreach (var descriptor in services
                     .Where(descriptor => descriptor.ServiceType == typeof(IHostedService)
                                          && descriptor.ImplementationType == typeof(OutboxProcessor))
                     .ToArray())
        {
            services.Remove(descriptor);
        }
    }

    private void ReplaceDbContext<TContext>(
        IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder>? configure = null)
        where TContext : DbContext
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<TContext>));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }

        // Only DbContextOptions<TContext> is removed, so the interceptor
        // registrations made by AddInfrastructure survive and stay resolvable.
        services.AddDbContext<TContext>((serviceProvider, options) =>
        {
            options.UseSqlServer(_msSqlContainer.GetConnectionString());
            configure?.Invoke(serviceProvider, options);
        });
    }

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();

        // Materialise the host now rather than on the first CreateClient(): its
        // startup is what applies the migrations, and Respawn reads the schema when
        // the checkpoint is created. Creating it against an empty database would
        // yield a checkpoint that resets nothing — silently, which is worse than
        // no reset at all.
        using (var _ = CreateClient())
        {
        }

        _connection = new SqlConnection(_msSqlContainer.GetConnectionString());
        await _connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(
            _connection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.SqlServer,
                SchemasToInclude = ["dbo"],
                // Both DbContexts write their applied migrations here. Wiping it
                // would leave the database's data inconsistent with its schema.
                TablesToIgnore = [new Table("__EFMigrationsHistory")]
            });
    }

    /// <summary>
    /// Empties every table but the migration history, so a test starts from a known
    /// state instead of inheriting whatever its predecessors left behind.
    /// </summary>
    public Task ResetDatabaseAsync() => _respawner.ResetAsync(_connection);

    public new async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _msSqlContainer.StopAsync();
        await base.DisposeAsync();
    }
}
