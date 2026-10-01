using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vulcan.Infrastructure;

namespace Vulcan.Infrastructure.Tests;

/// <summary>
/// Mock database instance. Uses an in-memory sqlite connection.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private SqliteConnection Connection { get; set; } = null!;

    public async Task InitializeAsync()
    {
        Connection = new SqliteConnection("Filename=:memory:");
        await Connection.OpenAsync();
    }

    public async Task DisposeAsync()
    {
        await Connection.DisposeAsync();
    }

    public VulcanDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(Connection)
            .Options;
        return new VulcanDbContext(options);
    }
}
