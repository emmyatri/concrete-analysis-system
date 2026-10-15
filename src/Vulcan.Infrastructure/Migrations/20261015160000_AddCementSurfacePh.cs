using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulcan.Infrastructure.Migrations;

public partial class AddCementSurfacePh : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "CementSurfacePh",
            table: "Batch",
            type: "TEXT",
            precision: 18,
            scale: 2,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CementSurfacePh",
            table: "Batch");
    }
}
