using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingHub.Shared.Infrastructure.ThirdParty.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class UseFullTimestampPrecision : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Widening a precision keeps every existing value: the stored instants are unchanged,
        /// they simply stop being rounded from now on. Rows written before this migration keep
        /// the 10 ms granularity they were stored with, which no backfill can recover.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedOn",
                table: "Training",
                type: "datetime2(7)",
                precision: 7,
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(2)",
                oldPrecision: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedOn",
                table: "Training",
                type: "datetime2(7)",
                precision: 7,
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(2)",
                oldPrecision: 2);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedOn",
                table: "Trainer",
                type: "datetime2(7)",
                precision: 7,
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(2)",
                oldPrecision: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedOn",
                table: "Trainer",
                type: "datetime2(7)",
                precision: 7,
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(2)",
                oldPrecision: 2);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Unlike <c>Up</c>, this one loses data: narrowing back to two fractional digits rounds
        /// every stored instant to its 10 ms bucket, irreversibly.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedOn",
                table: "Training",
                type: "datetime2(2)",
                precision: 2,
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(7)",
                oldPrecision: 7,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedOn",
                table: "Training",
                type: "datetime2(2)",
                precision: 2,
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(7)",
                oldPrecision: 7);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedOn",
                table: "Trainer",
                type: "datetime2(2)",
                precision: 2,
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(7)",
                oldPrecision: 7,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedOn",
                table: "Trainer",
                type: "datetime2(2)",
                precision: 2,
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(7)",
                oldPrecision: 7);
        }
    }
}
