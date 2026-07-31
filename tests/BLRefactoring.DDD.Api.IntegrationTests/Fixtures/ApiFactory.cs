using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore;
using BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore.Interceptors;
using BLRefactoring.Shared.Infrastructure.ThirdParty.Identity;
using Respawn;
using Respawn.Graph;
using Testcontainers.MsSql;
using Xunit;

namespace BLRefactoring.DDD.Api.IntegrationTests.Fixtures;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
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
        });

        builder.UseEnvironment("Development");
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

[CollectionDefinition("Api")]
public class ApiCollection : ICollectionFixture<ApiFactory>;
