using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulcan.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddConstraints : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ValidationLimit_Key",
            table: "ValidationLimit");

        migrationBuilder.DropIndex(
            name: "IX_Specimen_BatchId",
            table: "Specimen");

        migrationBuilder.CreateIndex(
            name: "IX_ValidationLimit_Key_Severity",
            table: "ValidationLimit",
            columns: new[] { "Key", "Severity" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Specimen_BatchId_Label",
            table: "Specimen",
            columns: new[] { "BatchId", "Label" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ValidationLimit_Key_Severity",
            table: "ValidationLimit");

        migrationBuilder.DropIndex(
            name: "IX_Specimen_BatchId_Label",
            table: "Specimen");

        migrationBuilder.CreateIndex(
            name: "IX_ValidationLimit_Key",
            table: "ValidationLimit",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Specimen_BatchId",
            table: "Specimen",
            column: "BatchId");
    }
}
