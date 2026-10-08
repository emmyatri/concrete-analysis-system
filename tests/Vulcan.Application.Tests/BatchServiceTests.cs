using Vulcan.Application.Services;
using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Services;
using Vulcan.Infrastructure;
using Vulcan.Infrastructure.Services;

namespace Vulcan.Application.Tests;

public class BatchServiceTests(DatabaseFixture fixture)
    : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture = fixture;

    // Verifies that a complete valid batch passes all batch, composition, and
    // chemistry validation and persists the batch with its related data.
    [Fact]
    public async Task CreateAsync_ValidCompleteBatch_SavesAndLoadsBatch()
    {
        await using var db = _fixture.CreateDbContext();

        var limitProvider = new LimitProvider(db);
        var limitEvaluator = new LimitEvaluator(limitProvider);

        var repository = new BatchRepository(db);

        var service = CreateService(
            db,
            repository,
            limitEvaluator);

        var batch = CreateValidBatch();

        var result = await service.CreateAsync(batch);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);

        var saved = await repository.GetByNumberAsync("VM-TEST-001");

        Assert.NotNull(saved);
        Assert.NotNull(saved.Composition);
        Assert.NotNull(saved.CementOxideAnalysis);

        Assert.Equal(12m, saved.Composition.CementPct);
        Assert.Equal(5m, saved.CementOxideAnalysis.AluminaPct);
    }

    // Verifies that invalid batch fields are returned as validation errors
    // and that the rejected batch is not persisted.
    [Fact]
    public async Task CreateAsync_InvalidBatch_ReturnsFieldErrorsAndDoesNotSave()
    {
        await using var db = _fixture.CreateDbContext();

        var limitProvider = new LimitProvider(db);
        var limitEvaluator = new LimitEvaluator(limitProvider);

        var repository = new BatchRepository(db);

        var service = CreateService(
            db,
            repository,
            limitEvaluator);

        var batch = CreateValidBatch();
        batch.BatchNumber = "BAD BATCH!";
        batch.FinalWeightLb = 0m;

        var result = await service.CreateAsync(batch);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);

        var saved = await repository.GetByNumberAsync("BADBATCH!");
        Assert.Null(saved);
    }

    // Verifies that a batch can be retrieved by batch number with its
    // composition and cement oxide analysis included.
    [Fact]
    public async Task GetByNumberAsync_ExistingBatch_LoadsCompleteBatch()
    {
        await using var db = _fixture.CreateDbContext();

        var limitProvider = new LimitProvider(db);
        var limitEvaluator = new LimitEvaluator(limitProvider);
        var repository = new BatchRepository(db);

        var service = CreateService(
            db,
            repository,
            limitEvaluator);

        var batch = CreateValidBatch();
        batch.BatchNumber = "VM-LOAD-001";

        var createResult = await service.CreateAsync(batch);
        Assert.True(createResult.IsSuccess);

        var loaded = await service.GetByNumberAsync("VM-LOAD-001");

        Assert.NotNull(loaded);
        Assert.Equal("VM-LOAD-001", loaded.BatchNumber);
        Assert.NotNull(loaded.Composition);
        Assert.NotNull(loaded.CementOxideAnalysis);
    }

    // Verifies that valid changes to batch, composition, and chemistry data
    // are validated and persisted when an existing batch is updated.
    [Fact]
    public async Task UpdateAsync_ValidBatch_PersistsChanges()
    {
        await using var db = _fixture.CreateDbContext();

        var limitProvider = new LimitProvider(db);
        var limitEvaluator = new LimitEvaluator(limitProvider);
        var repository = new BatchRepository(db);

        var service = CreateService(
            db,
            repository,
            limitEvaluator);

        var batch = CreateValidBatch();
        batch.BatchNumber = "VM-UPDATE-001";

        var createResult = await service.CreateAsync(batch);
        Assert.True(createResult.IsSuccess);

        var updatedBatch = CreateValidBatch();
        updatedBatch.BatchNumber = "VM-UPDATE-001";
        updatedBatch.FinalWeightLb = 35m;
        updatedBatch.ExpectedFinalBreakPsi = 6000;

        updatedBatch.Composition!.CementPct = 13m;
        updatedBatch.Composition.WaterPct = 16m;

        updatedBatch.CementOxideAnalysis!.AluminaPct = 6m;
        updatedBatch.CementOxideAnalysis.IronOxidePct = 4m;

        var updateResult =
            await service.UpdateAsync("VM-UPDATE-001", updatedBatch);

        Assert.True(updateResult.IsSuccess);
        Assert.Empty(updateResult.Errors);

        var loaded =
            await service.GetByNumberAsync("VM-UPDATE-001");

        Assert.NotNull(loaded);

        Assert.Equal(35m, loaded.FinalWeightLb);
        Assert.Equal(6000, loaded.ExpectedFinalBreakPsi);

        Assert.NotNull(loaded.Composition);
        Assert.Equal(13m, loaded.Composition.CementPct);
        Assert.Equal(16m, loaded.Composition.WaterPct);

        Assert.NotNull(loaded.CementOxideAnalysis);
        Assert.Equal(6m, loaded.CementOxideAnalysis.AluminaPct);
        Assert.Equal(4m, loaded.CementOxideAnalysis.IronOxidePct);
    }

    // Verifies that an invalid update is rejected before persistence and
    // leaves the previously stored batch data unchanged.
    [Fact]
    public async Task UpdateAsync_InvalidBatch_ReturnsFieldErrorsAndDoesNotPersistChanges()
    {
        await using var db = _fixture.CreateDbContext();

        var limitProvider = new LimitProvider(db);
        var limitEvaluator = new LimitEvaluator(limitProvider);
        var repository = new BatchRepository(db);

        var service = CreateService(
            db,
            repository,
            limitEvaluator);

        var batch = CreateValidBatch();
        batch.BatchNumber = "VM-UPDATE-INVALID";

        var createResult = await service.CreateAsync(batch);
        Assert.True(createResult.IsSuccess);

        var updatedBatch = CreateValidBatch();
        updatedBatch.BatchNumber = "VM-UPDATE-INVALID";
        updatedBatch.FinalWeightLb = 0m;

        var updateResult =
            await service.UpdateAsync("VM-UPDATE-INVALID", updatedBatch);

        Assert.False(updateResult.IsSuccess);
        Assert.NotEmpty(updateResult.Errors);

        var loaded =
            await service.GetByNumberAsync("VM-UPDATE-INVALID");

        Assert.NotNull(loaded);
        Assert.Equal(30m, loaded.FinalWeightLb);
    }

    // Verifies that search uses partial batch-number matching, excludes
    // nonmatching batches, returns newest results first, and limits pages to 50.
    [Fact]
    public async Task SearchAsync_PartialMatch_ReturnsNewestFirstAndFiftyPerPage()
    {
        await using var db = _fixture.CreateDbContext();

        var limitProvider = new LimitProvider(db);
        var limitEvaluator = new LimitEvaluator(limitProvider);
        var repository = new BatchRepository(db);

        var service = CreateService(
            db,
            repository,
            limitEvaluator);

        for (var i = 1; i <= 55; i++)
        {
            db.Batch.Add(new Batch
            {
                BatchNumber = $"VM-SEARCH-{i:D3}",
                CastAt = new DateTime(
                    2026, 10, 5, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i)
            });
        }

        db.Batch.Add(new Batch
        {
            BatchNumber = "UNRELATED-001",
            CastAt = new DateTime(
                2026, 10, 5, 0, 0, 0, DateTimeKind.Utc)
        });

        await db.SaveChangesAsync();

        var firstPage = await service.SearchAsync(
            "vm-search", null, null, null, null, null, 1);

        var secondPage = await service.SearchAsync(
            "vm-search", null, null, null, null, null, 2);

        Assert.Equal(50, firstPage.Count);
        Assert.Equal(5, secondPage.Count);

        Assert.Equal("VM-SEARCH-055", firstPage[0].BatchNumber);
        Assert.Equal("VM-SEARCH-006", firstPage[^1].BatchNumber);

        Assert.Equal("VM-SEARCH-005", secondPage[0].BatchNumber);
        Assert.Equal("VM-SEARCH-001", secondPage[^1].BatchNumber);

        Assert.DoesNotContain(
            firstPage,
            batch => batch.BatchNumber == "UNRELATED-001");
    }

    // Verifies that loading a batch returns the full persisted aggregate,
    // including its specimens, break results, and curing readings.
    [Fact]
    public async Task GetByNumberAsync_ExistingBatch_LoadsSpecimensBreaksAndReadings()
    {
        await using var db = _fixture.CreateDbContext();

        var limitProvider = new LimitProvider(db);
        var limitEvaluator = new LimitEvaluator(limitProvider);
        var repository = new BatchRepository(db);

        var service = CreateService(
            db,
            repository,
            limitEvaluator);

        var batch = CreateValidBatch();
        batch.BatchNumber = "VM-AGGREGATE-001";

        var createResult = await service.CreateAsync(batch);
        Assert.True(createResult.IsSuccess);

        var specimen = new Specimen
        {
            BatchId = batch.Id,
            Label = "C1",
            LengthIn = 12m,
            WidthIn = 6m,
            TestAgeDays = TestAgeDays.Day3
        };

        db.Specimen.Add(specimen);
        await db.SaveChangesAsync();

        db.BreakResult.Add(new BreakResult
        {
            SpecimenId = specimen.Id,
            BrokenAt = new DateTime(
                2026, 10, 5, 12, 0, 0, DateTimeKind.Utc),
            StrengthPsi = 5000
        });

        db.CuringReading.Add(new CuringReading
        {
            SpecimenId = specimen.Id,
            TakenAt = new DateTime(
                2026, 10, 5, 10, 0, 0, DateTimeKind.Utc),
            InternalTempF = 75m,
            AmbientTempF = 72m,
            MoisturePct = 5m,
            RelativeHumidityPct = 50m,
            ResistivityOhmPerFt = 1000m,
            Co2Ppm = 400m
        });

        await db.SaveChangesAsync();

        var loaded =
            await service.GetByNumberAsync("VM-AGGREGATE-001");

        Assert.NotNull(loaded);

        var loadedSpecimen = Assert.Single(loaded.Specimens);

        Assert.Equal("C1", loadedSpecimen.Label);

        Assert.NotNull(loadedSpecimen.BreakResult);
        Assert.Equal(
            5000,
            loadedSpecimen.BreakResult.StrengthPsi);

        var loadedReading =
            Assert.Single(loadedSpecimen.CuringReadings);

        Assert.Equal(75m, loadedReading.InternalTempF);
        Assert.Equal(400m, loadedReading.Co2Ppm);
    }

    // Verifies that an update cannot silently change the batch number used
    // to identify the existing persisted batch.
    [Fact]
    public async Task UpdateAsync_ChangedBatchNumber_ReturnsFieldError()
    {
        await using var db = _fixture.CreateDbContext();

        var limitProvider = new LimitProvider(db);
        var limitEvaluator = new LimitEvaluator(limitProvider);
        var repository = new BatchRepository(db);

        var service = CreateService(
            db,
            repository,
            limitEvaluator);

        var batch = CreateValidBatch();
        batch.BatchNumber = "VM-UPDATE-NUMBER";

        var createResult = await service.CreateAsync(batch);
        Assert.True(createResult.IsSuccess);

        var updatedBatch = CreateValidBatch();
        updatedBatch.BatchNumber = "VM-DIFFERENT-NUMBER";

        var result = await service.UpdateAsync(
            "VM-UPDATE-NUMBER",
            updatedBatch);

        Assert.False(result.IsSuccess);

        Assert.Contains(
            result.Errors,
            x => x.Key == "Batch.BatchNumber");

        var original =
            await service.GetByNumberAsync("VM-UPDATE-NUMBER");

        Assert.NotNull(original);

        var renamed =
            await service.GetByNumberAsync("VM-DIFFERENT-NUMBER");

        Assert.Null(renamed);
    }

    // Verifies that a warning does not block batch creation, is returned to the
    // caller, and is persisted against the generated batch ID.
    [Fact]
    public async Task CreateAsync_Warning_SavesBatchAndPersistsWarning()
    {
        await using var db = _fixture.CreateDbContext();

        var limitProvider = new LimitProvider(db);
        var limitEvaluator = new LimitEvaluator(limitProvider);
        var repository = new BatchRepository(db);

        var service = CreateService(
            db,
            repository,
            limitEvaluator);

        var batch = CreateValidBatch();
        batch.BatchNumber = "VM-WARN-001";
        batch.MixWaterPh = 6.5m;

        var result = await service.CreateAsync(batch);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);

        Assert.Contains(
            result.Warnings,
            warning => warning.Key == LimitKeys.MixWaterPh);

        Assert.True(batch.Id > 0);

        var warningRecord = Assert.Single(
            db.RecordWarning.Where(
                warning =>
                    warning.EntityType == nameof(Batch) &&
                    warning.EntityId == batch.Id &&
                    warning.LimitKey == LimitKeys.MixWaterPh));

        Assert.Equal(6.5m, warningRecord.Value);
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

            Composition = new Composition
            {
                CementPct = 12m,
                ScmPct = 0m,
                ScmType = ScmType.None,
                CoarseAggregatePct = 40m,
                FineAggregatePct = 25m,
                WaterPct = 15m
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

    private static BatchService CreateService(
        VulcanDbContext db,
        BatchRepository repository,
        LimitEvaluator limitEvaluator)
    {
        return new BatchService(
            repository,
            new RecordWarningRepository(db),
            new BatchValidator(limitEvaluator),
            new CompositionValidator(limitEvaluator),
            new CementOxideValidator(limitEvaluator),
            new WaterCementRatioCalculator(
                new SpecificGravityProvider(db),
                limitEvaluator),
            new TestClock(
                new DateTime(
                    2026, 10, 5, 18, 0, 0, DateTimeKind.Utc)));
    }
}
