using Microsoft.EntityFrameworkCore;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;

namespace Vulcan.Infrastructure.Tests;

public class UnitTest1(DatabaseFixture dbFixture) : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _dbFixture = dbFixture;

    [Fact]
    public async Task Complete_Batch_can_be_created_and_read()
    {
        await using var db = _dbFixture.CreateDbContext();
        await db.Database.EnsureCreatedAsync();

        // for the purposes of the test, this disregards validation limits
        var batch = new Batch
        {
            BatchNumber = "VM-BN-19401293410394",
            CastAt = DateTime.UtcNow,
            FinalWeightLb = 30.12m,
            ExpectedFinalBreakPsi = 3500,
            CementType = CementType.TypeII,
            MixWaterPh = 3.4m,
            IsSynthetic = true,
            Composition = new Composition
            {
                CementPct = 56m,
                ScmPct = 12m,
                ScmType = ScmType.FlyAsh,
                CoarseAggregatePct = 4m,
                FineAggregatePct = 0,
                WaterPct = 2m,
            },
            CementOxideAnalysis = new CementOxideAnalysis
            {
                AluminaPct = 63m,
                IronOxidePct = 0m,
                MagnesiumOxidePct = 4,
                SulfurTrioxidePct = 3.5m,
            },
            Specimens =
            [
                new Specimen
                {
                    Label = "WOW",
                    LengthIn = 11.39m,
                    WidthIn = 5.98m,
                    TestAgeDays = TestAgeDays.Day3,
                    IsSynthetic = true,
                },
                new Specimen
                {
                    Label = "WOW2",
                    LengthIn = 11.99m,
                    WidthIn = 6,
                    TestAgeDays = TestAgeDays.Day28,
                    IsSynthetic = true,
                    BreakResult = new BreakResult
                    {
                        BrokenAt = DateTime.UtcNow.AddHours(-2),
                        CreatedAtUtc = DateTime.UtcNow,
                        CreatedBy = "sys",
                        StrengthPsi = 3001,
                    },
                    CuringReadings =
                    [
                        new CuringReading
                        {
                            AmbientTempF = 68.2m,
                            InternalTempF = 50m,
                            Co2Ppm = 1,
                            CreatedAtUtc = DateTime.UtcNow,
                            CreatedBy = "sys",
                            ModifiedAtUtc = DateTime.UtcNow.AddDays(2),
                            MoisturePct = 4.2m,
                            RelativeHumidityPct = 45m,
                            ResistivityOhmPerFt = 40.3m,
                        }
                    ]
                },
            ]
        };

        db.Add(batch);
        await db.SaveChangesAsync();

        var retrievedBatch = db.Batch
            .Include(x => x.CementOxideAnalysis)
            .Include(x => x.Composition)
            .Include(x => x.Specimens)
            .ThenInclude(x => x.CuringReadings)
            .Include(x => x.Specimens)
            .ThenInclude(x => x.BreakResult)
            .First();
        // autoincrement starts at 1
        Assert.Equal(1, retrievedBatch.Id);
        Assert.Equal(batch.CementOxideAnalysis.IronOxidePct, retrievedBatch.CementOxideAnalysis!.IronOxidePct);
        Assert.Equal(batch.CementType, retrievedBatch.CementType);
        Assert.Contains(retrievedBatch.Specimens, x => x.Label == "WOW");
        Assert.Contains(retrievedBatch.Specimens
                .Where(s => s.BreakResult != null),
            (s => s.BreakResult!.SpecimenId == s.Id));
    }
}
