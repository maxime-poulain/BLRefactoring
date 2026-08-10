using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainerPhotoSanitization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nullable, and every existing row keeps that null on purpose: a portrait stored before
            // ADR 0063 was never stripped, nothing here can prove otherwise, and the public
            // endpoint refuses to serve what it cannot prove. Backfilling a date would be inventing
            // evidence.
            migrationBuilder.AddColumn<DateTime>(
                name: "PhotoSanitizedOnUtc",
                table: "Trainer",
                type: "datetime2(7)",
                precision: 7,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoSanitizedOnUtc",
                table: "Trainer");
        }
    }
}
