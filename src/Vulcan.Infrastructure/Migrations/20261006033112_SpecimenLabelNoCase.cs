using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulcan.Infrastructure.Migrations;

/// <inheritdoc />
public partial class SpecimenLabelNoCase : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Label",
            table: "Specimen",
            type: "TEXT",
            maxLength: 20,
            nullable: false,
            collation: "NOCASE",
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 20);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Label",
            table: "Specimen",
            type: "TEXT",
            maxLength: 20,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 20,
            oldCollation: "NOCASE");
    }
}
