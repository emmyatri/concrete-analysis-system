using Microsoft.EntityFrameworkCore;

using Vulcan.Infrastructure.Seed;

namespace Vulcan.Infrastructure.Tests;

public class SyntheticDataLoaderTests(DatabaseFixture dbFixture)
    : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _dbFixture = dbFixture;

    // Verifies that the approved M1-20 fixture can be loaded from the
    // embedded JSON resource and contains all nine synthetic batches.
    [Fact]
    public void Load_ReturnsApprovedSyntheticDataset()
    {
        var batches = SyntheticDataLoader.Load();

        Assert.Equal(9, batches.Count);

        // Batch and Specimen are the entities that explicitly store
        // IsSynthetic in the approved schema.
        Assert.All(batches, batch => Assert.True(batch.IsSynthetic));
        Assert.All(
            batches.SelectMany(batch => batch.Specimens),
            specimen => Assert.True(specimen.IsSynthetic));
    }

    // M1-24 requires synthetic data to be loaded when the database is empty.
    // This also verifies that EF Core can persist the complete nested
    // Batch -> Specimen -> BreakResult/CuringReading graph from the fixture.
    [Fact]
    public async Task Seed_EmptyDatabase_LoadsSyntheticBatches()
    {
        await using var db = _dbFixture.CreateDbContext();
        await db.Database.MigrateAsync();

        SyntheticDataSeeder.Seed(db);

        Assert.Equal(9, await db.Batch.CountAsync());

        // Verify that the complete approved M1-20 dataset was persisted,
        // not just the nine parent batch records.
        Assert.Equal(81, await db.Specimen.CountAsync());
        Assert.Equal(81, await db.BreakResult.CountAsync());
        Assert.Equal(567, await db.CuringReading.CountAsync());

        Assert.All(
            await db.Batch.ToListAsync(),
            batch => Assert.True(batch.IsSynthetic));
    }

    // M1-24 requires seeding to be idempotent. Once batches exist,
    // running the seeder again must not insert a second copy.
    [Fact]
    public async Task Seed_CalledTwice_DoesNotCreateDuplicates()
    {
        await using var db = _dbFixture.CreateDbContext();
        await db.Database.MigrateAsync();

        SyntheticDataSeeder.Seed(db);
        SyntheticDataSeeder.Seed(db);

        Assert.Equal(9, await db.Batch.CountAsync());
    }
}
