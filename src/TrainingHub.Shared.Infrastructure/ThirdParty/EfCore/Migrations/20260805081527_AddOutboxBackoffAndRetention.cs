using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxBackoffAndRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptOnUtc",
                table: "OutboxMessage",
                type: "datetime2(7)",
                precision: 7,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_Delivered",
                table: "OutboxMessage",
                column: "ProcessedOnUtc",
                filter: "[ProcessedOnUtc] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessage_Delivered",
                table: "OutboxMessage");

            migrationBuilder.DropColumn(
                name: "NextAttemptOnUtc",
                table: "OutboxMessage");
        }
    }
}
