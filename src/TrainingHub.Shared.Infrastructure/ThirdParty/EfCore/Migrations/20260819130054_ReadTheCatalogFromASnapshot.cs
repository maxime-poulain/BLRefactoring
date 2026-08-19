using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class ReadTheCatalogFromASnapshot : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// No table moves: the model is unchanged and this migration says one thing about the
        /// database itself. Under the server's default READ COMMITTED a reader takes shared locks,
        /// so the anonymous catalog read and the erasure cascade deleting the same index rows can
        /// form a lock cycle — and the read, being the cheaper transaction, is the one SQL Server
        /// kills. READ_COMMITTED_SNAPSHOT has a reader read the committed row version instead, so
        /// it takes no shared locks and can no longer be part of a cycle. See ADR 0094.
        /// <para>
        /// Three details of the statement are load-bearing. <c>suppressTransaction</c>, because EF
        /// wraps a migration in one and ALTER DATABASE cannot run inside a transaction. <c>WITH
        /// ROLLBACK IMMEDIATE</c>, because the setting needs exclusive access to the database and
        /// without it the statement waits for every other session indefinitely. And the guard,
        /// because a database that already reads from a snapshot has no sessions to terminate.
        /// </para>
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                """
                IF (SELECT [is_read_committed_snapshot_on] FROM [sys].[databases] WHERE [database_id] = DB_ID()) = 0
                    EXEC('ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE');
                """,
                suppressTransaction: true);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                """
                IF (SELECT [is_read_committed_snapshot_on] FROM [sys].[databases] WHERE [database_id] = DB_ID()) = 1
                    EXEC('ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE');
                """,
                suppressTransaction: true);
    }
}
