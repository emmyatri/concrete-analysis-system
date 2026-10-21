using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulcan.Infrastructure.Migrations;

/// <inheritdoc />
public partial class UpdateFinalWeightLb : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // update the existing limit that changed
        migrationBuilder.Sql(@"
                UPDATE ValidationLimit
                SET Min = 10,
                    Max = 40,
                    Message = 'Sample weight must be within the range [10-40]',
                    Source = 'Client, M1 demo (Oct 9)'
                WHERE Key = 'Batch.FinalWeightLb'
                    AND Severity = 'Reject';
            ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // undo the update to the previous values
        migrationBuilder.Sql(@"
                UPDATE ValidationLimit
                SET Min = 0.01,
                    Max = NULL,
                    Message = 'Weight must be greater than 0',
                    Source = 'schema.md'
                WHERE Key = 'Batch.FinalWeightLb'
                    AND Severity = 'Reject';
            ");
        // remove the added warning limit.
        // not needed in `up` since the db seeds the difference
        migrationBuilder.Sql(@"
                DELETE FROM ValidationLimit
                WHERE Key = 'Batch.FinalWeightLb'
                    AND Severity = 'Warn';
            ");
    }
}
