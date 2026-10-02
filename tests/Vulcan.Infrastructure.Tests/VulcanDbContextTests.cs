using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;

namespace Vulcan.Infrastructure.Tests;

public class VulcanDbContextTests(DatabaseFixture dbFixture) : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _dbFixture = dbFixture;

    [Fact]
    public async Task Complete_Batch_can_be_created_and_read()
    {
        await using var db = _dbFixture.CreateDbContext();
        await db.Database.MigrateAsync();

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

        await using var readDb = _dbFixture.CreateDbContext();
        var retrievedBatch = await readDb.Batch
            .Include(x => x.CementOxideAnalysis)
            .Include(x => x.Composition)
            .Include(x => x.Specimens)
            .ThenInclude(x => x.CuringReadings)
            .Include(x => x.Specimens)
            .ThenInclude(x => x.BreakResult)
            .SingleAsync(x => x.Id == batch.Id);

        retrievedBatch.Id.ShouldBe(batch.Id);
        retrievedBatch.CementOxideAnalysis!.IronOxidePct.ShouldBe(batch.CementOxideAnalysis.IronOxidePct);
        retrievedBatch.CementType.ShouldBe(batch.CementType);
        retrievedBatch.Specimens.ShouldContain(x => x.Label == "WOW");
        retrievedBatch.Specimens
            .Where(s => s.BreakResult != null)
            .ShouldContain(s => s.BreakResult!.SpecimenId == s.Id);
    }

    [Fact]
    public async Task Batch_cannot_have_specimens_with_same_label()
    {
        await using var db = _dbFixture.CreateDbContext();
        await db.Database.MigrateAsync();

        var batch = new Batch
        {
            BatchNumber = "VM-BN-1035910935",
            Specimens =
            [
                new Specimen
                {
                    Label = "IMACOPY",
                },
                new Specimen
                {
                    Label = "IMACOPY"
                }
            ]
        };

        db.Add(batch);
        await db
            .SaveChangesAsync()
            .ShouldThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Different_batches_can_have_specimens_with_same_label()
    {
        await using var db = _dbFixture.CreateDbContext();
        await db.Database.MigrateAsync();

        List<Batch> batches =
        [
            new()
            {
                BatchNumber = "VM-BN-91835198519835",
                Specimens =
                [
                    new Specimen
                    {
                        Label = "IMACOPY",
                    }
                ]
            },
            new()
            {
                BatchNumber = "VM-BN-5921395813985",
                Specimens =
                [
                    new Specimen
                    {
                        Label = "IMACOPY"
                    }
                ]
            }
        ];

        db.AddRange(batches);
        await db
            .SaveChangesAsync()
            .ShouldNotThrowAsync();
    }

    [Fact]
    public async Task Batches_cannot_have_same_number_case_insensitive()
    {
        await using var db = _dbFixture.CreateDbContext();
        await db.Database.MigrateAsync();

        List<Batch> batches =
        [
            new()
            {
                BatchNumber = "VM-BN-555"
            },
            new()
            {
                BatchNumber = "vM-Bn-555"
            }
        ];

        db.AddRange(batches);
        await db
            .SaveChangesAsync()
            .ShouldThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Specimen_cannot_have_two_break_results()
    {
        await using var db = _dbFixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var specimen = new Specimen
        {
            Label = "WOW",
            BreakResult = new BreakResult
            {
                BrokenAt = DateTime.UtcNow,
            }
        };

        var batch = new Batch
        {
            BatchNumber = "VM-BN-53815985aoeugmgoae",
            Specimens = [specimen]
        };

        db.Add(batch);
        await db.SaveChangesAsync();

        var existing = await db.BreakResult
            .SingleAsync();
        existing.SpecimenId.ShouldBe(specimen.Id);

        var secondBreakResult = new BreakResult
        {
            SpecimenId = specimen.Id
        };

        // forces ef core to send SQL, triggering the unique constraint.
        // otherwise ef will fail silently when it goes to resolve changes.
        db.ChangeTracker.Clear();
        db.Add(secondBreakResult);

        var ex = await Should.ThrowAsync<DbUpdateException>(
            () => db.SaveChangesAsync());
        ex.InnerException.ShouldBeOfType<SqliteException>();
    }

    [Fact]
    public async Task All_validation_keys_are_in_seed_data()
    {
        await using var db = _dbFixture.CreateDbContext();
        await db.Database.MigrateAsync();

        db.SeedValidationLimits();

        // yeah https://stackoverflow.com/a/21230891
        List<string> validationKeys =
        [
            .. typeof(LimitKeys)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.FieldType == typeof(string) && f is { IsInitOnly: false, IsPublic: true })
                .Select(f => (string)f.GetValue(null)!)
        ];

        await using var readDb = _dbFixture.CreateDbContext();
        await readDb.Database.MigrateAsync();

        // each key should have an entry in the database
        validationKeys
            .ForEach(key =>
                readDb.ValidationLimit
                    .Any(x => x.Key == key)
                    .ShouldBeTrue());
        // number of unique keys should match
        readDb.ValidationLimit
            .Select(x => x.Key)
            .Distinct()
            .Count()
            .ShouldBe(validationKeys.Count);
    }

    [Fact]
    public async Task All_specific_gravity_materials_are_in_seed_data()
    {
        await using var db = _dbFixture.CreateDbContext();
        await db.Database.MigrateAsync();

        db.SeedSpecificGravities();

        List<string> materials = [.. typeof(Material).GetEnumNames()];

        await using var readDb = _dbFixture.CreateDbContext();
        await readDb.Database.MigrateAsync();

        // all specific gravity materials should be in the db
        materials
            .ForEach(material =>
                readDb.SpecificGravity
                    .Select(x => x.Material.ToString())
                    .ToList() // otherwise the next call fails to convert to SQL
                    .Any(x => string.Equals(x, material))
                    .ShouldBeTrue());
        // number of materials should match
        readDb.SpecificGravity
            .Count()
            .ShouldBe(materials.Count);
    }
}
