using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TrainingHub.Shared.Api.Health;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using TrainingHub.Shared.Infrastructure.ThirdParty.Identity;
using Xunit;

namespace TrainingHub.Shared.Api.Tests.Health;

/// <summary>
/// The schema probe, on both of its answers (ADR 0045).
/// </summary>
/// <remarks>
/// The branch that matters is the red one, and it is the branch an integration test cannot reach:
/// the suites run one SQL Server container whose schema every test shares, and Respawn deliberately
/// leaves <c>__EFMigrationsHistory</c> alone, so reaching "a migration is pending" over HTTP would
/// mean mutilating the history the rest of the run depends on. So the probe is exercised directly,
/// against SQLite in memory.
/// <para>
/// SQLite answers the only question asked here — <c>GetPendingMigrationsAsync</c> compares the
/// migrations the assembly declares with the rows of the history table — and it answers it without
/// a container, which is what keeps this fact in the suite that runs without Docker. The schema
/// itself is never built: the migrations are scaffolded for SQL Server, and building it would prove
/// something else. The green branch stamps the history through EF's own
/// <see cref="IHistoryRepository"/> rather than hand-written SQL, so the two tests agree with the
/// runtime about what "applied" is recorded as.
/// </para>
/// <para>
/// Both contexts share one connection, exactly as they share one database in production: a single
/// history table holds the rows of both.
/// </para>
/// </remarks>
public sealed class PendingMigrationsHealthCheckTests
{
    /// <summary>
    /// Check health, answers unhealthy, while the history is empty.
    /// </summary>
    /// <remarks>
    /// A database with no history table is the extreme of the case the record is about: every
    /// migration is pending, both contexts are behind, and an instance in that state must stop
    /// receiving traffic. The description names both contexts and their counts, because an operator
    /// sent to apply half of what is missing has been told half the truth.
    /// </remarks>
    [Fact]
    public async Task CheckHealth_AnswersUnhealthy_WhileTheHistoryIsEmpty()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var business = BusinessContextOn(connection);
        await using var identity = IdentityContextOn(connection);

        var result = await new PendingMigrationsHealthCheck(business, identity)
            .CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy,
            "a host whose schema is behind must stop receiving traffic rather than serve requests " +
            "that will fail on a missing column");

        result.Description.Should()
            .Contain($"{nameof(TrainingContext)}: {business.Database.GetMigrations().Count()} pending")
            .And.Contain($"{nameof(TrainingIdentityDbContext)}: {identity.Database.GetMigrations().Count()} pending");
    }

    /// <summary>
    /// Check health, answers healthy, once every migration is recorded.
    /// </summary>
    /// <remarks>
    /// The other half of "re-reads on every poll, and caches nothing": the same probe over the same
    /// connection answers differently once the history says the schema caught up, which is what lets
    /// readiness go green after a migration applied out of band — without a restart.
    /// </remarks>
    [Fact]
    public async Task CheckHealth_AnswersHealthy_OnceEveryMigrationIsRecorded()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var business = BusinessContextOn(connection);
        await using var identity = IdentityContextOn(connection);

        var probe = new PendingMigrationsHealthCheck(business, identity);

        (await probe.CheckHealthAsync(new HealthCheckContext())).Status
            .Should().Be(HealthStatus.Unhealthy, "nothing has been applied yet");

        await RecordEveryMigrationAsync(business);
        await RecordEveryMigrationAsync(identity);

        var result = await probe.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy,
            "the probe reads the history on every poll and caches nothing, so it goes green on " +
            "its own once the schema catches up");
        result.Description.Should().BeNull("a healthy probe has nothing to tell an operator");
    }

    private static TrainingContext BusinessContextOn(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<TrainingContext>().UseSqlite(connection).Options);

    private static TrainingIdentityDbContext IdentityContextOn(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<TrainingIdentityDbContext>().UseSqlite(connection).Options);

    // What `dotnet ef database update` leaves behind, minus the schema: the history rows alone.
    // Stamped through EF's own repository so that "applied" means here what it means at runtime.
    private static async Task RecordEveryMigrationAsync(DbContext context)
    {
        var history = context.GetService<IHistoryRepository>();
        var runtime = typeof(DbContext).Assembly.GetName().Version!.ToString();

        await context.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript());

        foreach (var migration in context.Database.GetMigrations())
        {
            await context.Database.ExecuteSqlRawAsync(
                history.GetInsertScript(new HistoryRow(migration, runtime)));
        }
    }
}
