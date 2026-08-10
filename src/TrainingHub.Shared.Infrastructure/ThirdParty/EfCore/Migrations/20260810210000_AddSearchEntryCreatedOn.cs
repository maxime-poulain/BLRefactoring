using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchEntryCreatedOn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOnUtc",
                table: "TrainingSearchEntry",
                type: "datetime2(7)",
                precision: 7,
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Backfilled from the write model rather than left at the default: the column holds
            // the training's own age (ADR 0071), and every entry describes a training the same
            // database already holds. Without this, the existing catalog would sort as one block
            // of year-one rows until the next replay reached each of them.
            migrationBuilder.Sql("""
                UPDATE [TrainingSearchEntry]
                SET [CreatedOnUtc] = [Training].[CreatedOn]
                FROM [TrainingSearchEntry]
                INNER JOIN [Training] ON [Training].[Id] = [TrainingSearchEntry].[TrainingId];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSearchEntry_OfferedNewest",
                table: "TrainingSearchEntry",
                columns: new[] { "IsPublished", "IsTrainerHidden", "CreatedOnUtc", "TrainingId" },
                descending: new[] { false, false, true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrainingSearchEntry_OfferedNewest",
                table: "TrainingSearchEntry");

            migrationBuilder.DropColumn(
                name: "CreatedOnUtc",
                table: "TrainingSearchEntry");
        }
    }
}
