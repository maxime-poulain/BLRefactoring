using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainingHub.Shared.Infrastructure.ThirdParty.EfCore;
using Xunit;

namespace TrainingHub.Api.TestKit;

/// <summary>
/// How the database this host migrated answers a reader, against a real SQL Server.
/// </summary>
/// <remarks>
/// The catalog's read was found dead on the CI runner, a 500 carrying SQL Server's error 1205: it
/// had been chosen as the deadlock victim against the erasure cascade deleting the same index rows
/// beside it. Under the server's default READ COMMITTED a reader takes shared locks and can be part
/// of a lock cycle; under READ_COMMITTED_SNAPSHOT it reads the committed row version and cannot.
/// A migration turns the setting on (ADR 0094), and this asks the database whether it took.
/// <para>
/// The property rather than the race: a deadlock that appeared once in thirty runs cannot be made
/// to fail on demand, and a test that tried would be a coin flip pretending to be a proof. What can
/// be proven is the thing that makes the cycle impossible, and this is it.
/// </para>
/// <para>
/// Run against both suites: the migration lives in the shared infrastructure and both hosts apply
/// it, so a regression would show on whichever host nobody was testing.
/// </para>
/// </remarks>
/// <typeparam name="TFactory">The suite's fixture.</typeparam>
public abstract class CatalogSnapshotIsolationTest<TFactory>(TFactory factory) : IntegrationTest<TFactory>(factory)
    where TFactory : IResettableDatabase, IHttpClientSource, IServiceScopeSource
{
    /// <summary>
    /// The migrated database, reads from a snapshot.
    /// </summary>
    [Fact]
    public async Task TheMigratedDatabase_ReadsFromASnapshot()
    {
        using var scope = Factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrainingContext>();

        // sys.databases rather than a behavior: the setting is what the migration claims to have
        // changed, and the server is the only thing that can say whether it did.
        var readsFromASnapshot = await context.Database
            .SqlQueryRaw<bool>(
                "SELECT [is_read_committed_snapshot_on] AS [Value] FROM [sys].[databases] WHERE [database_id] = DB_ID()")
            .SingleAsync();

        readsFromASnapshot.Should().BeTrue(
            "a reader that takes shared locks can be a deadlock victim, and the catalog's read is "
            + "an anonymous page nobody should be able to turn into a 500 by erasing their account");
    }
}
