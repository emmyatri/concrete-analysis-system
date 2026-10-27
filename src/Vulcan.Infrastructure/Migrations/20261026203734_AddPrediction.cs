using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulcan.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddPrediction : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddUniqueConstraint(
            name: "AK_CalibrationVersion_Version",
            table: "CalibrationVersion",
            column: "Version");

        migrationBuilder.CreateTable(
            name: "Prediction",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                BatchId = table.Column<int>(type: "INTEGER", nullable: false),
                CalibrationVersion = table.Column<string>(type: "TEXT", nullable: false),
                Basis = table.Column<string>(type: "TEXT", nullable: false),
                HorizonDays = table.Column<int>(type: "INTEGER", nullable: false),
                EstimatedPsi = table.Column<decimal>(type: "TEXT", nullable: true),
                EquivalentAgeDays = table.Column<decimal>(type: "TEXT", nullable: true),
                AdjustmentStage = table.Column<string>(type: "TEXT", nullable: true),
                UnadjustedPsi = table.Column<decimal>(type: "TEXT", nullable: true),
                IsSynthetic = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                ModifiedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                ModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Prediction", x => x.Id);
                table.ForeignKey(
                    name: "FK_Prediction_Batch_BatchId",
                    column: x => x.BatchId,
                    principalTable: "Batch",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Prediction_CalibrationVersion_CalibrationVersion",
                    column: x => x.CalibrationVersion,
                    principalTable: "CalibrationVersion",
                    principalColumn: "Version",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Prediction_BatchId",
            table: "Prediction",
            column: "BatchId");

        migrationBuilder.CreateIndex(
            name: "IX_Prediction_CalibrationVersion",
            table: "Prediction",
            column: "CalibrationVersion");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Prediction");

        migrationBuilder.DropUniqueConstraint(
            name: "AK_CalibrationVersion_Version",
            table: "CalibrationVersion");
    }
}
