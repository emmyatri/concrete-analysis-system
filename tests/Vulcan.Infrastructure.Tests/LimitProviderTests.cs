using Microsoft.EntityFrameworkCore;
using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Interfaces;
using Vulcan.Infrastructure.Services;

namespace Vulcan.Infrastructure.Tests;

public class LimitProviderTests(DatabaseFixture dbFixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task LimitProvider_can_read_both_limits()
    {
        await using var db = dbFixture.CreateDbContext();
        await db.Database.MigrateAsync();

        List<ValidationLimit> limits =
        [
            new()
            {
                Key = "Primary",
                Severity = Severity.Reject,
                Min = 0,
                Max = 100,
            },
            new()
            {
                Key = "Primary",
                Severity = Severity.Warn,
                Min = 25,
                Max = 50
            }
        ];

        db.AddRange(limits);
        await db.SaveChangesAsync();

        await using var readDb = dbFixture.CreateDbContext();
        await readDb.Database.MigrateAsync();

        ILimitProvider limitProvider = new LimitProvider(readDb);
        LimitSet limitSet = limitProvider.Get("Primary");

        limitSet.Warn.ShouldNotBeNull();
        limitSet.Warn.Min.ShouldBe(25);
        limitSet.Warn.Max.ShouldBe(50);

        limitSet.Reject.ShouldNotBeNull();
        limitSet.Reject.Min.ShouldBe(0);
        limitSet.Reject.Max.ShouldBe(100);
    }

    [Fact]
    public async Task Limit_provider_rejects_unknown_key()
    {
        await using var db = dbFixture.CreateDbContext();
        await db.Database.MigrateAsync();

        ILimitProvider limitProvider = new LimitProvider(db);
        Should.Throw<InvalidOperationException>(() => limitProvider.Get("Unknown.Key"));
    }
}
