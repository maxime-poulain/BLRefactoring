using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationReasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WithholdingReason",
                table: "Training",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspensionReason",
                table: "Trainer",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WithholdingReason",
                table: "Training");

            migrationBuilder.DropColumn(
                name: "SuspensionReason",
                table: "Trainer");
        }
    }
}
