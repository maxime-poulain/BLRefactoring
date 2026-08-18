using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingHub.Shared.Infrastructure.ThirdParty.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailVerificationToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailVerificationToken",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2(7)", precision: 7, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailVerificationToken", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_EmailVerificationToken_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationToken_TokenHash",
                table: "EmailVerificationToken",
                column: "TokenHash",
                unique: true);

            // Every account that predates verification is grandfathered: the alternative would
            // freeze the catalog of every existing trainer on deploy day, for a proof the product
            // never asked of them. The honest cost, recorded in ADR 0090: after this line,
            // EmailConfirmed means "verified or predates verification", and the distinction is
            // unrecoverable. Down() leaves the flags where they are, for the same reason.
            migrationBuilder.Sql("UPDATE AspNetUsers SET EmailConfirmed = 1 WHERE EmailConfirmed = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailVerificationToken");
        }
    }
}
