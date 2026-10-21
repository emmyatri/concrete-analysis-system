using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vulcan.Infrastructure;

namespace Vulcan.Application.Tests;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();

        await using var dbContext = CreateDbContext();

        await dbContext.Database.MigrateAsync();

        dbContext.SeedValidationLimits();
        dbContext.SeedSpecificGravities();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    public VulcanDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new VulcanDbContext(options);
    }
}
