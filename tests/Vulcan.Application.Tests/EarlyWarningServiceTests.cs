using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Vulcan.Application.Contracts;
using Vulcan.Application.Services;
using Vulcan.Application.Services.EarlyWarning;
using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Services;
using Vulcan.Infrastructure;
using Vulcan.Infrastructure.Interceptors;
using Vulcan.Infrastructure.Seed;
using Vulcan.Infrastructure.Services;

namespace Vulcan.Application.Tests;

public class EarlyWarningServiceTests()
{
    private static VulcanDbContext GetDbContext(
        SqliteConnection conn)
    {
        var opts = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(conn)
            .AddInterceptors(
                new PredictionSaveChangesInterceptor(
                    db => new CalibrationProvider(db),
                    new StrengthEstimateService(),
                    new NullLogger<PredictionSaveChangesInterceptor>())
            );
        return new VulcanDbContext(opts.Options);
    }

    private static async Task InitTestDb(VulcanDbContext db)
    {
        await db.Database.MigrateAsync();
        db.SeedCalibrationVersionV1(); // required for prediction
        db.SeedValidationLimits();     // required for early warning service
        db.SeedSpecificGravities();    // required for water cement ratio calculator
        await db.SaveChangesAsync();
    }

    private static EarlyWarningService SetupEarlyWarningService(VulcanDbContext db)
    {
        var limitProvider = new LimitProvider(db);
        var limitEvaluator = new LimitEvaluator(limitProvider);
        return new EarlyWarningService(
            new PredictionRepository(db),
            new RecordWarningRepository(db),
            limitProvider,
            new WaterCementRatioCalculator(
                new SpecificGravityProvider(db),
                limitEvaluator));
    }

    private static BatchService SetupBatchService(VulcanDbContext db)
    {
        var limitEvaluator = new LimitEvaluator(new LimitProvider(db));
        return new BatchService(
            new BatchRepository(db),
            new RecordWarningRepository(db),
            new BatchValidator(limitEvaluator),
            new CompositionValidator(limitEvaluator),
            new CementOxideValidator(limitEvaluator),
            new WaterCementRatioCalculator(
                new SpecificGravityProvider(db),
                limitEvaluator),
            new TestClock(DateTime.UtcNow));
    }

    private static SpecimenService SetupSpecimenService(VulcanDbContext db)
    {
        var limitEvaluator = new LimitEvaluator(new LimitProvider(db));
        return new SpecimenService(
            new SpecimenRepository(db),
            new SpecimenValidator(limitEvaluator),
            new BreakResultValidator(limitEvaluator),
            new TestClock(DateTime.UtcNow));
    }

    private static CuringService SetupCuringService(VulcanDbContext db)
    {
        return new CuringService(
            new CuringRepository(db),
            new CuringReadingValidator(
                new LimitEvaluator(new LimitProvider(db))),
            new TestClock(DateTime.UtcNow));
    }

    [Fact]
    public async Task Synthetic_batch_vm_syn_006_is_flagged_for_wcm()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var db = GetDbContext(connection);
        await InitTestDb(db);

        // save through the batch service to get record warnings
        var batchService = SetupBatchService(db);
        List<Batch> batches = SyntheticDataLoader.Load();

        foreach (var batch in batches)
        {
            await batchService.CreateAsync(batch);
        }

        var syn = db.Batch
            .Where(b => b.BatchNumber == "VM-SYN-006")
            .FirstOrDefault()
            .ShouldNotBeNull();

        var evaluator = SetupEarlyWarningService(db);

        var result = await evaluator.Evaluate(syn);
        result.AtRisk.ShouldBeTrue();
        // VM-SYN-006 should be flagged for WCM percent
        result.EarlyWarnings
            .Any(ew => ew.LimitKey == LimitKeys.ExpectedFinalVsWcmPct)
            .ShouldBeTrue();
        // and no other batches should
        var otherBatches = db.Batch.Where(b => b.BatchNumber != syn.BatchNumber).ToList();
        foreach (var batch in otherBatches)
        {
            var warnings = await evaluator.Evaluate(batch);
            warnings.EarlyWarnings
                .Where(ew => ew.LimitKey == LimitKeys.ExpectedFinalVsWcmPct)
                .ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task Synthetic_batches_match_the_m2_demo()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var db = GetDbContext(connection);
        await InitTestDb(db);
        var batchService = SetupBatchService(db);
        foreach (var batch in SyntheticDataLoader.Load())
        {
            await batchService.CreateAsync(batch);
        }
        var evaluator = SetupEarlyWarningService(db);

        var syn006 = await evaluator.Evaluate(db.Batch.Single(b => b.BatchNumber == "VM-SYN-006"));
        syn006.AtRisk.ShouldBeTrue();
        syn006.Projected28DayPsi.ShouldBe(4449d);
        syn006.ShortfallPct.ShouldBe(28.24);
        syn006.EarlyWarnings[0].Warning.ShouldBe(
            "Projected 28-day strength of 4,449 PSI is 28.24% below the expected 6,200 PSI");
        syn006.EarlyWarnings.ShouldContain(w => w.Warning ==
                                                "Expected final of 6,200 PSI is 67.6% above the 3,700 PSI typical for a w/cm of 0.603");
        syn006.EarlyWarnings.ShouldNotContain(w => w.LimitKey == "Break.StrengthPsi");
        syn006.EarlyWarnings.ShouldNotContain(w => w.Warning.Contains("TODO"));

        var syn009 = await evaluator.Evaluate(db.Batch.Single(b => b.BatchNumber == "VM-SYN-009"));
        syn009.AtRisk.ShouldBeFalse();
        syn009.EarlyWarnings.Single().LimitKey.ShouldBe("Batch.MixWaterPh");

        var syn001 = await evaluator.Evaluate(db.Batch.Single(b => b.BatchNumber == "VM-SYN-001"));
        syn001.EarlyWarnings.ShouldBeEmpty();
    }

    [Fact]
    public async Task Synthetic_batch_vm_syn_006_is_flagged_before_break()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var db = GetDbContext(connection);
        await InitTestDb(db);

        // save through the batch service to get record warnings
        var batchService = SetupBatchService(db);
        List<Batch> batches = SyntheticDataLoader.Load();

        var vmSyn = batches
            .FirstOrDefault(b => b.BatchNumber == "VM-SYN-006")
            .ShouldNotBeNull();
        // scrub break results
        foreach (var specimen in vmSyn.Specimens)
        {
            specimen.BreakResult = null;
        }
        // save
        await batchService.CreateAsync(vmSyn);

        var syn = db.Batch
            .FirstOrDefault(b => b.BatchNumber == "VM-SYN-006")
            .ShouldNotBeNull();

        var evaluator = SetupEarlyWarningService(db);

        var result = await evaluator.Evaluate(syn);
        // Without breaks the projection is 6,235 PSI (above target), so it's flagged but not at risk.
        result.AtRisk.ShouldBeFalse();
        // VM-SYN-006 should be flagged for WCM percent
        result.EarlyWarnings
            .Any(ew => ew.LimitKey == LimitKeys.ExpectedFinalVsWcmPct)
            .ShouldBeTrue();
    }

    [Fact]
    public async Task On_track_batch_is_not_flagged()
    {
        var castAt = new DateTime(2026, 10, 5, 17, 0, 0, DateTimeKind.Utc);
        var onTrackBatch = CreateOnTrackBatch(castAt);

        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var db = GetDbContext(connection);
        await InitTestDb(db);

        // use respective services to pass through validation checks
        var batchService = SetupBatchService(db);
        var specimenService = SetupSpecimenService(db);
        var curingService = SetupCuringService(db);

        await batchService.CreateAsync(onTrackBatch);

        var savedBatch = await db.Batch
            .Include(b => b.Composition)
            .Include(b => b.CementOxideAnalysis)
            .FirstAsync(b => b.BatchNumber == "VM-TEST-001");
        savedBatch.ShouldNotBeNull();

        var specimen = new Specimen
        {
            Label = "D28-1",
            LengthIn = 12m,
            WidthIn = 5.94m,
            TestAgeDays = TestAgeDays.Day28,
            IsSynthetic = false
        };
        await specimenService.AddAsync(savedBatch.Id, specimen);

        await curingService.AddReadingAsync(specimen.Id, new CuringReading
        {
            TakenAt = castAt.AddDays(1),
            InternalTempF = 75m,
            AmbientTempF = 70m,
            MoisturePct = 90m,
            RelativeHumidityPct = 95m,
            ResistivityOhmPerFt = 20m,
            Co2Ppm = 400m
        });

        await curingService.AddReadingAsync(specimen.Id, new CuringReading
        {
            TakenAt = castAt.AddDays(2),
            InternalTempF = 75m,
            AmbientTempF = 70m,
            MoisturePct = 90m,
            RelativeHumidityPct = 95m,
            ResistivityOhmPerFt = 20m,
            Co2Ppm = 400m
        });

        await specimenService.RecordBreakAsync(specimen.Id, new BreakResult
        {
            BrokenAt = castAt.AddDays(2),
            StrengthPsi = 5000
        });

        var batchWithPredictions = await db.Batch
            .Include(b => b.Specimens)
                .ThenInclude(s => s.CuringReadings)
            .Include(b => b.Specimens)
                .ThenInclude(s => s.BreakResult)
            .FirstAsync(b => b.BatchNumber == "VM-TEST-001");

        var evaluator = SetupEarlyWarningService(db);
        var result = await evaluator.Evaluate(batchWithPredictions);

        result.AtRisk.ShouldBeFalse();
        result.EarlyWarnings
            .Any(ew =>
                ew.LimitKey == LimitKeys.PredictionShortfallPct
                || ew.LimitKey == LimitKeys.PredictionMinimumPsi
                || ew.LimitKey == LimitKeys.ExpectedFinalVsWcmPct)
            .ShouldBeFalse();
    }

    private static Batch CreateOnTrackBatch(DateTime castAt)
    {
        return new Batch
        {
            BatchNumber = "VM-TEST-001",
            CastAt = castAt,
            FinalWeightLb = 30m,
            ExpectedFinalBreakPsi = 5000,
            CementType = CementType.TypeI,
            MixWaterPh = 7.2m,
            CementSurfacePh = 7.5m,
            Composition = new Composition
            {
                CementPct = 12m,
                ScmPct = 0m,
                ScmType = ScmType.None,
                CoarseAggregatePct = 40m,
                FineAggregatePct = 25m,
                WaterPct = 16m
            },
            CementOxideAnalysis = new CementOxideAnalysis
            {
                AluminaPct = 5m,
                IronOxidePct = 3m,
                MagnesiumOxidePct = 2m,
                SulfurTrioxidePct = 2m
            }
        };
    }

    [Fact]
    public async Task Every_early_warning_reads_as_plain_language()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var db = GetDbContext(connection);
        await InitTestDb(db);
        var batchService = SetupBatchService(db);
        foreach (var batch in SyntheticDataLoader.Load())
        {
            await batchService.CreateAsync(batch);
        }
        var evaluator = SetupEarlyWarningService(db);

        var warnings = new List<EarlyWarningDTO>();
        foreach (var batch in db.Batch.ToList())
        {
            warnings.AddRange((await evaluator.Evaluate(batch)).EarlyWarnings);
        }

        warnings.ShouldNotBeEmpty();
        warnings.ShouldAllBe(w => !string.IsNullOrWhiteSpace(w.Warning));
        warnings.ShouldAllBe(w => !w.Warning.Contains(w.LimitKey));
        warnings.ShouldAllBe(w => !w.Warning.Contains("TODO"));

        // Prediction and target warnings carry the batch's real numbers.
        warnings
            .Where(w => w.LimitKey.StartsWith("Prediction.") || w.LimitKey == LimitKeys.ExpectedFinalVsWcmPct)
            .ShouldAllBe(w => w.Warning.Contains(" PSI") && w.Warning.Any(char.IsDigit));
    }
}
