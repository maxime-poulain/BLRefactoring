using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingHub.Shared.Infrastructure.ThirdParty.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountLanguage",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountLanguage", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_AccountLanguage_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // No backfill, deliberately. An account that predates this table has stated no
            // preference, and inventing one would be a guess dressed as a choice — the read port
            // answers SupportedLanguages.Default for a missing row, which is exactly what those
            // accounts get today (ADR 0091).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountLanguage");
        }
    }
}
