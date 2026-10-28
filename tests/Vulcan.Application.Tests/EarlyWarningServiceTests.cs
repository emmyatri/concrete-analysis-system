using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Vulcan.Application.Services;
using Vulcan.Application.Services.EarlyWarning;
using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
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

        batches.ForEach(async batch => await batchService.CreateAsync(batch));

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
    public void On_track_batch_is_not_flagged()
    {

    }
}