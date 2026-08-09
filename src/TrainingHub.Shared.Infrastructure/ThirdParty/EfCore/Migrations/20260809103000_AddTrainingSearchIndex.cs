using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrainingSearchEntry",
                columns: table => new
                {
                    TrainingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    IsTrainerHidden = table.Column<bool>(type: "bit", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TrainerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingSearchEntry", x => x.TrainingId);
                });

            migrationBuilder.CreateTable(
                name: "TrainingSearchTerm",
                columns: table => new
                {
                    TrainingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Term = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingSearchTerm", x => new { x.TrainingId, x.Term });
                    table.ForeignKey(
                        name: "FK_TrainingSearchTerm_TrainingSearchEntry_TrainingId",
                        column: x => x.TrainingId,
                        principalTable: "TrainingSearchEntry",
                        principalColumn: "TrainingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSearchEntry_Offered",
                table: "TrainingSearchEntry",
                columns: new[] { "IsPublished", "IsTrainerHidden", "Title", "TrainingId" });

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSearchEntry_TrainerId",
                table: "TrainingSearchEntry",
                column: "TrainerId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSearchTerm_Term",
                table: "TrainingSearchTerm",
                columns: new[] { "Term", "TrainingId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrainingSearchTerm");

            migrationBuilder.DropTable(
                name: "TrainingSearchEntry");
        }
    }
}
