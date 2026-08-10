using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class AddLifecycleStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The defaults are the whole of this migration. Every row that already exists is a
            // training on offer and a trainer in good standing — that is what the system meant
            // before it could say it — so backfilling anything else would silently withdraw a
            // catalog or sanction a trainer nobody sanctioned. The generated defaults were the
            // empty string, which no status answers to and which would have made every existing
            // row throw on read (ADR 0050).
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Training",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Published");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Trainer",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Training");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Trainer");
        }
    }
}
