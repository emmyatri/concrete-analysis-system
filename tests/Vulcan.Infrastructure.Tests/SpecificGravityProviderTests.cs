using Microsoft.EntityFrameworkCore;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Interfaces;
using Vulcan.Infrastructure.Services;

namespace Vulcan.Infrastructure.Tests;

public class SpecificGravityProviderTests(DatabaseFixture dbFixture)
    : IClassFixture<DatabaseFixture>
{
    [Theory]
    [InlineData(Material.Cement, "3.15")]
    [InlineData(Material.Water, "1.00")]
    [InlineData(Material.FlyAsh, "2.35")]
    [InlineData(Material.Slag, "2.90")]
    [InlineData(Material.SilicaFume, "2.35")]
    public async Task SpecificGravityProvider_reads_stored_value(
        Material material,
        string expectedValue)
    {
        await using var db = dbFixture.CreateDbContext();
        await db.Database.MigrateAsync();

        var expected = decimal.Parse(expectedValue);

        db.SpecificGravity.Add(
            new SpecificGravity
            {
                Material = material,
                Value = expected
            });

        await db.SaveChangesAsync();

        await using var readDb = dbFixture.CreateDbContext();

        ISpecificGravityProvider provider =
            new SpecificGravityProvider(readDb);

        var actual = provider.Get(material);

        actual.ShouldBe(expected);
    }
}
