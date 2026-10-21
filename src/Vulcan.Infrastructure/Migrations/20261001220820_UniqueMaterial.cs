using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulcan.Infrastructure.Migrations;

/// <inheritdoc />
public partial class UniqueMaterial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_SpecificGravity_Material",
            table: "SpecificGravity",
            column: "Material",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_SpecificGravity_Material",
            table: "SpecificGravity");
    }
}
