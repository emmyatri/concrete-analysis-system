using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulcan.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Batch",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                BatchNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, collation: "NOCASE"),
                CastAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                FinalWeightLb = table.Column<decimal>(type: "TEXT", nullable: false),
                ExpectedFinalBreakPsi = table.Column<int>(type: "INTEGER", nullable: false),
                CementType = table.Column<string>(type: "TEXT", nullable: false),
                MixWaterPh = table.Column<decimal>(type: "TEXT", nullable: false),
                IsSynthetic = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                ModifiedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                ModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Batch", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "RecordWarning",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                EntityType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                EntityId = table.Column<int>(type: "INTEGER", nullable: false),
                FieldName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                LimitKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Value = table.Column<decimal>(type: "TEXT", nullable: false),
                Message = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                ModifiedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                ModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RecordWarning", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "SpecificGravity",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Material = table.Column<string>(type: "TEXT", nullable: false),
                Value = table.Column<decimal>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SpecificGravity", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ValidationLimit",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Severity = table.Column<string>(type: "TEXT", nullable: false),
                Min = table.Column<decimal>(type: "TEXT", nullable: true),
                Max = table.Column<decimal>(type: "TEXT", nullable: true),
                Unit = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                Message = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Source = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                ModifiedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                ModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ValidationLimit", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "CementOxideAnalysis",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                BatchId = table.Column<int>(type: "INTEGER", nullable: false),
                AluminaPct = table.Column<decimal>(type: "TEXT", nullable: false),
                IronOxidePct = table.Column<decimal>(type: "TEXT", nullable: false),
                MagnesiumOxidePct = table.Column<decimal>(type: "TEXT", nullable: false),
                SulfurTrioxidePct = table.Column<decimal>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                ModifiedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                ModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CementOxideAnalysis", x => x.Id);
                table.ForeignKey(
                    name: "FK_CementOxideAnalysis_Batch_BatchId",
                    column: x => x.BatchId,
                    principalTable: "Batch",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Composition",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                BatchId = table.Column<int>(type: "INTEGER", nullable: false),
                CementPct = table.Column<decimal>(type: "TEXT", nullable: false),
                ScmPct = table.Column<decimal>(type: "TEXT", nullable: false),
                ScmType = table.Column<string>(type: "TEXT", nullable: false),
                CoarseAggregatePct = table.Column<decimal>(type: "TEXT", nullable: false),
                FineAggregatePct = table.Column<decimal>(type: "TEXT", nullable: false),
                WaterPct = table.Column<decimal>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                ModifiedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                ModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Composition", x => x.Id);
                table.ForeignKey(
                    name: "FK_Composition_Batch_BatchId",
                    column: x => x.BatchId,
                    principalTable: "Batch",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Specimen",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                BatchId = table.Column<int>(type: "INTEGER", nullable: false),
                Label = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                LengthIn = table.Column<decimal>(type: "TEXT", nullable: false),
                WidthIn = table.Column<decimal>(type: "TEXT", nullable: false),
                TestAgeDays = table.Column<string>(type: "TEXT", nullable: false),
                IsSynthetic = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                ModifiedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                ModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Specimen", x => x.Id);
                table.ForeignKey(
                    name: "FK_Specimen_Batch_BatchId",
                    column: x => x.BatchId,
                    principalTable: "Batch",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "BreakResult",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                SpecimenId = table.Column<int>(type: "INTEGER", nullable: false),
                BrokenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                StrengthPsi = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                ModifiedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                ModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BreakResult", x => x.Id);
                table.ForeignKey(
                    name: "FK_BreakResult_Specimen_SpecimenId",
                    column: x => x.SpecimenId,
                    principalTable: "Specimen",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "CuringReading",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                SpecimenId = table.Column<int>(type: "INTEGER", nullable: false),
                TakenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                InternalTempF = table.Column<decimal>(type: "TEXT", nullable: false),
                AmbientTempF = table.Column<decimal>(type: "TEXT", nullable: false),
                MoisturePct = table.Column<decimal>(type: "TEXT", nullable: false),
                RelativeHumidityPct = table.Column<decimal>(type: "TEXT", nullable: false),
                ResistivityOhmPerFt = table.Column<decimal>(type: "TEXT", nullable: false),
                Co2Ppm = table.Column<decimal>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                ModifiedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                ModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CuringReading", x => x.Id);
                table.ForeignKey(
                    name: "FK_CuringReading_Specimen_SpecimenId",
                    column: x => x.SpecimenId,
                    principalTable: "Specimen",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Batch_BatchNumber",
            table: "Batch",
            column: "BatchNumber",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_BreakResult_SpecimenId",
            table: "BreakResult",
            column: "SpecimenId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CementOxideAnalysis_BatchId",
            table: "CementOxideAnalysis",
            column: "BatchId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Composition_BatchId",
            table: "Composition",
            column: "BatchId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CuringReading_SpecimenId",
            table: "CuringReading",
            column: "SpecimenId");

        migrationBuilder.CreateIndex(
            name: "IX_Specimen_BatchId",
            table: "Specimen",
            column: "BatchId");

        migrationBuilder.CreateIndex(
            name: "IX_ValidationLimit_Key",
            table: "ValidationLimit",
            column: "Key",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "BreakResult");

        migrationBuilder.DropTable(
            name: "CementOxideAnalysis");

        migrationBuilder.DropTable(
            name: "Composition");

        migrationBuilder.DropTable(
            name: "CuringReading");

        migrationBuilder.DropTable(
            name: "RecordWarning");

        migrationBuilder.DropTable(
            name: "SpecificGravity");

        migrationBuilder.DropTable(
            name: "ValidationLimit");

        migrationBuilder.DropTable(
            name: "Specimen");

        migrationBuilder.DropTable(
            name: "Batch");
    }
}
