using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
    public void Cold_cured_batch_is_at_risk_for_low_temp()
    { }

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
        result.AtRisk.ShouldBeTrue();
        // VM-SYN-006 should be flagged for WCM percent
        result.EarlyWarnings
            .Any(ew => ew.LimitKey == LimitKeys.ExpectedFinalVsWcmPct)
            .ShouldBeTrue();
    }

    [Fact]
    public async Task On_track_batch_is_not_flagged()
    {
        var castAt = DateTime.UtcNow;
        var onTrackBatch = CreateValidBatch();
        // var onTrackSpecimens = [];
        // var onTrackBreakResults = [];
        // var onTrackCuringReadings = [];

        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var db = GetDbContext(connection);
        await InitTestDb(db);

        var batchService = SetupBatchService(db);
        var specimenService = SetupSpecimenService(db);
        var curingService = SetupCuringService(db);

        await batchService.CreateAsync(onTrackBatch);
    }

    private static Batch CreateValidBatch()
    {
        return new Batch
        {
            BatchNumber = "VM-TEST-001",
            CastAt = new DateTime(
                2026, 10, 5, 17, 0, 0, DateTimeKind.Utc),
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
}
