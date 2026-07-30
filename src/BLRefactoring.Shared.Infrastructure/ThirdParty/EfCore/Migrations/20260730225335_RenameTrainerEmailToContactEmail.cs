using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BLRefactoring.Shared.Infrastructure.ThirdParty.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class RenameTrainerEmailToContactEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Email",
                table: "Trainer",
                newName: "ContactEmail");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ContactEmail",
                table: "Trainer",
                newName: "Email");
        }
    }
}
