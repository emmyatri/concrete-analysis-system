using System.Runtime.InteropServices.JavaScript;
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
        db.SeedValidationLimits(); // required for early warning service
        db.SeedSpecificGravities(); // required for water cement ratio calculator
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
        // The same path the app uses to load the demo data, including its saved warnings.
        SyntheticDataSeeder.Seed(db);
        var evaluator = SetupEarlyWarningService(db);

        async Task<EarlyWarningEvaluation> Evaluate(string batchNumber) =>
            await evaluator.Evaluate(db.Batch.Single(b => b.BatchNumber == batchNumber));

        // High w/c: at risk, with the w/c input warning and the expected-vs-w/cm check.
        var syn006 = await Evaluate("VM-SYN-006");
        syn006.AtRisk.ShouldBeTrue();
        syn006.Projected28DayPsi.ShouldBe(4733d);
        syn006.ShortfallPct.ShouldBe(23.66);
        syn006.EarlyWarnings[0].Warning.ShouldBe(
            "Projected 28-day strength of 4,733 PSI is 23.66% below the expected 6,200 PSI");
        syn006.EarlyWarnings.ShouldContain(w => w.LimitKey == LimitKeys.WaterCementRatio);
        syn006.EarlyWarnings.ShouldContain(w => w.Warning ==
                                                "Expected final of 6,200 PSI is 67.6% above the 3,700 PSI typical for a w/cm of 0.603");
        syn006.EarlyWarnings.ShouldNotContain(w => w.LimitKey == LimitKeys.StrengthPsi);

        // Cold-cured: at risk, with one ambient reason (not one per reading).
        var syn002 = await Evaluate("VM-SYN-002");
        syn002.AtRisk.ShouldBeTrue();
        syn002.Projected28DayPsi.ShouldBe(5028d);
        syn002.ShortfallPct.ShouldBe(10.21);
        syn002.EarlyWarnings.Count(w => w.LimitKey == LimitKeys.AmbientTempF).ShouldBe(1);

        // Moisture loss: at risk after the 3- and 7-day breaks.
        // Low moisture is also flagged as an input warning.
        var syn003 = await Evaluate("VM-SYN-003");
        syn003.AtRisk.ShouldBeTrue();
        syn003.Projected28DayPsi.ShouldBe(4535d);

        syn003.EarlyWarnings.ShouldContain(w => w.LimitKey == LimitKeys.PredictionShortfallPct);

        syn003.EarlyWarnings.Count(w => w.LimitKey == LimitKeys.MoisturePct).ShouldBe(1);

        // pH: flagged on entry but on track.
        var syn009 = await Evaluate("VM-SYN-009");
        syn009.AtRisk.ShouldBeFalse();
        syn009.EarlyWarnings.Single().LimitKey.ShouldBe(LimitKeys.MixWaterPh);

        foreach (var onTrack in new[] { "VM-SYN-001", "VM-SYN-004", "VM-SYN-005", "VM-SYN-007", "VM-SYN-008" })
        {
            (await Evaluate(onTrack)).EarlyWarnings.ShouldBeEmpty(onTrack);
        }

        // 150 cold ambient readings, 94 moisture readings,
        // one w/c warning, and one pH warning.
        db.RecordWarning.Count().ShouldBe(246);
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
            MoisturePct = 3m,
            RelativeHumidityPct = 95m,
            ResistivityOhmPerFt = 20m,
            Co2Ppm = 400m
        });

        await curingService.AddReadingAsync(specimen.Id, new CuringReading
        {
            TakenAt = castAt.AddDays(2),
            InternalTempF = 75m,
            AmbientTempF = 70m,
            MoisturePct = 3m,
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

    // Client-decisions §14.1. Two cylinders 100 PSI either side of the average, so the band is checked
    // against the average for that age, not each break.
    [Theory]
    [InlineData(TestAgeDays.Day3, 800, true)]
    [InlineData(TestAgeDays.Day3, 1500, false)]
    [InlineData(TestAgeDays.Day7, 1700, true)]
    [InlineData(TestAgeDays.Day7, 2400, false)]
    public async Task Low_3_and_7_day_break_averages_put_the_batch_at_risk(
        TestAgeDays age,
        int averagePsi,
        bool flagged)
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var db = GetDbContext(connection);
        await InitTestDb(db);
        var evaluator = SetupEarlyWarningService(db);

        var result = await evaluator.Evaluate(
            BatchWithBreaks((age, averagePsi - 100), (age, averagePsi + 100)));

        var key = age == TestAgeDays.Day3 ? LimitKeys.BreakDay3WarnPct : LimitKeys.BreakDay7FailPct;
        result.EarlyWarnings.Any(w => w.LimitKey == key).ShouldBe(flagged);
        result.AtRisk.ShouldBe(flagged);
    }

    [Fact]
    public async Task Low_early_breaks_read_as_plain_language()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var db = GetDbContext(connection);
        await InitTestDb(db);
        var evaluator = SetupEarlyWarningService(db);

        var result = await evaluator.Evaluate(
            BatchWithBreaks((TestAgeDays.Day3, 800), (TestAgeDays.Day7, 1700), (TestAgeDays.Day28, 2500)));

        var day3 = result.EarlyWarnings.Single(w => w.LimitKey == LimitKeys.BreakDay3WarnPct);
        day3.Warning.ShouldBe(
            "3-day breaks averaged 800 PSI, below 1,200 PSI (40% of 3,000 PSI).");
        day3.Value.ShouldBe(26.7);
        day3.Limit.ShouldBe(40d);
        day3.Recommendation.ShouldBeNull();

        var day7 = result.EarlyWarnings.Single(w => w.LimitKey == LimitKeys.BreakDay7FailPct);
        day7.Warning.ShouldBe(
            "7-day breaks averaged 1,700 PSI, below 2,100 PSI (70% of 3,000 PSI). The batch will probably fail.");
        day7.Value.ShouldBe(56.7);
        day7.Limit.ShouldBe(70d);
        day7.Recommendation.ShouldBe(
            "Both early breaks are low. Vulcan's practice is to abandon this batch and start a new one.");

        // The 28-day break is checked by BreakResultValidator when it's saved, not here.
        result.EarlyWarnings.ShouldNotContain(w => w.LimitKey == LimitKeys.StrengthPsi);
    }

    private static Batch BatchWithBreaks(params (TestAgeDays Age, int Psi)[] breaks)
    {
        var castAt = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        return new Batch
        {
            BatchNumber = "VM-TEST-BANDS",
            CastAt = castAt,
            ExpectedFinalBreakPsi = 5000,
            CementType = CementType.TypeI,
            Specimens =
            [
                .. breaks.Select((b, i) => new Specimen
                {
                    Label = $"D{(int)b.Age}-{i + 1}",
                    LengthIn = 12m,
                    WidthIn = 6m,
                    TestAgeDays = b.Age,
                    BreakResult = new BreakResult
                    {
                        BrokenAt = castAt.AddDays((int)b.Age),
                        StrengthPsi = b.Psi
                    }
                })
            ]
        };
    }

    [Fact]
    public async Task Low_3_day_break_only_says_watch_and_never_abandon()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var db = GetDbContext(connection);
        await InitTestDb(db);
        var evaluator = SetupEarlyWarningService(db);

        var result = await evaluator.Evaluate(
            BatchWithBreaks((TestAgeDays.Day3, 800)));

        var day3 = result.EarlyWarnings.Single(w => w.LimitKey == LimitKeys.BreakDay3WarnPct);
        day3.Recommendation.ShouldBe("Watch this batch at the 7-day break.");
        result.EarlyWarnings.ShouldNotContain(w => w.Recommendation != null && w.Recommendation.Contains("abandon"));
    }
}
