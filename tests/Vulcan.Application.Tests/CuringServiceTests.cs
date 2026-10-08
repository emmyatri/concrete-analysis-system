using Microsoft.EntityFrameworkCore;
using Vulcan.Application.Services;
using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Services;
using Vulcan.Infrastructure.Services;

namespace Vulcan.Application.Tests;

public class CuringServiceTests(DatabaseFixture dbFixture)
    : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _dbFixture = dbFixture;

    // Verifies that a valid curing reading passes validation, is associated
    // with the requested specimen, and is persisted with its recorded values.
    [Fact]
    public async Task AddReadingAsync_ValidReading_SavesReading()
    {
        await using var db = _dbFixture.CreateDbContext();

        var limitProvider = new LimitProvider(db);
        var limitEvaluator = new LimitEvaluator(limitProvider);
        var repository = new CuringRepository(db);

        var clock = new TestClock(
            new DateTime(2026, 10, 5, 18, 0, 0, DateTimeKind.Utc));

        var service = new CuringService(
            repository,
            new CuringReadingValidator(limitEvaluator),
            clock);

        var batch = new Batch
        {
            BatchNumber = "VM-CURING-001",
            CastAt = new DateTime(
                2026, 10, 1, 12, 0, 0, DateTimeKind.Utc)
        };

        db.Batch.Add(batch);
        await db.SaveChangesAsync();

        var specimen = new Specimen
        {
            BatchId = batch.Id,
            Label = "C1",
            LengthIn = 12m,
            WidthIn = 6m
        };

        db.Specimen.Add(specimen);
        await db.SaveChangesAsync();

        var reading = new CuringReading
        {
            TakenAt = new DateTime(
                2026, 10, 2, 12, 0, 0, DateTimeKind.Utc),
            InternalTempF = 75m,
            AmbientTempF = 72m,
            MoisturePct = 5m,
            RelativeHumidityPct = 50m,
            ResistivityOhmPerFt = 1000m,
            Co2Ppm = 400m
        };

        var result =
            await service.AddReadingAsync(specimen.Id, reading);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);

        var saved = await db.CuringReading
            .SingleOrDefaultAsync(
                x => x.SpecimenId == specimen.Id);

        Assert.NotNull(saved);
        Assert.Equal(reading.TakenAt, saved.TakenAt);
        Assert.Equal(75m, saved.InternalTempF);
        Assert.Equal(72m, saved.AmbientTempF);
    }

    // Verifies that a warning-level curing value still saves, returns the
    // warning to the caller, and persists it against the generated reading ID.
    [Fact]
    public async Task AddReadingAsync_Warning_SavesAndPersistsWarning()
    {
        await using var db = _dbFixture.CreateDbContext();

        var limitProvider = new LimitProvider(db);
        var limitEvaluator = new LimitEvaluator(limitProvider);
        var repository = new CuringRepository(db);

        var clock = new TestClock(
            new DateTime(2026, 10, 5, 18, 0, 0, DateTimeKind.Utc));

        var service = new CuringService(
            repository,
            new CuringReadingValidator(limitEvaluator),
            clock);

        var batch = new Batch
        {
            BatchNumber = "VM-CURING-WARN-001",
            CastAt = new DateTime(
                2026, 10, 1, 12, 0, 0, DateTimeKind.Utc)
        };

        db.Batch.Add(batch);
        await db.SaveChangesAsync();

        var specimen = new Specimen
        {
            BatchId = batch.Id,
            Label = "C1",
            LengthIn = 12m,
            WidthIn = 6m
        };

        db.Specimen.Add(specimen);
        await db.SaveChangesAsync();

        var reading = new CuringReading
        {
            TakenAt = new DateTime(
                2026, 10, 2, 12, 0, 0, DateTimeKind.Utc),
            InternalTempF = 75m,
            AmbientTempF = 59m,
            MoisturePct = 5m,
            RelativeHumidityPct = 50m,
            ResistivityOhmPerFt = 1000m,
            Co2Ppm = 400m
        };

        var result =
            await service.AddReadingAsync(specimen.Id, reading);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);

        var warning = Assert.Single(result.Warnings);
        Assert.Equal(LimitKeys.AmbientTempF, warning.Key);

        Assert.True(reading.Id > 0);

        var saved = await db.CuringReading
            .SingleOrDefaultAsync(x => x.Id == reading.Id);

        Assert.NotNull(saved);
        Assert.Equal(59m, saved.AmbientTempF);

        var warningRecord = Assert.Single(
            db.RecordWarning.Where(
                warning =>
                    warning.EntityType == nameof(CuringReading) &&
                    warning.EntityId == reading.Id &&
                    warning.LimitKey == "Curing.AmbientTempF"));

        Assert.Equal(59m, warningRecord.Value);
    }

    // Verifies that all applicable curing validation errors are returned
    // together and that an invalid reading is not persisted.
    [Fact]
    public async Task AddReadingAsync_InvalidReading_ReturnsFieldErrorsAndDoesNotSave()
    {
        await using var db = _dbFixture.CreateDbContext();

        var limitProvider = new LimitProvider(db);
        var limitEvaluator = new LimitEvaluator(limitProvider);
        var repository = new CuringRepository(db);

        var clock = new TestClock(
            new DateTime(2026, 10, 5, 18, 0, 0, DateTimeKind.Utc));

        var service = new CuringService(
            repository,
            new CuringReadingValidator(limitEvaluator),
            clock);

        var batch = new Batch
        {
            BatchNumber = "VM-CURING-002",
            CastAt = new DateTime(
                2026, 10, 1, 12, 0, 0, DateTimeKind.Utc)
        };

        db.Batch.Add(batch);
        await db.SaveChangesAsync();

        var specimen = new Specimen
        {
            BatchId = batch.Id,
            Label = "C1",
            LengthIn = 12m,
            WidthIn = 6m
        };

        db.Specimen.Add(specimen);
        await db.SaveChangesAsync();

        var reading = new CuringReading
        {
            // Invalid: reading occurred before the batch was cast.
            TakenAt = new DateTime(
                2026, 9, 30, 12, 0, 0, DateTimeKind.Utc),

            // Invalid reject-level value.
            MoisturePct = -1m
        };

        var result =
            await service.AddReadingAsync(specimen.Id, reading);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);

        Assert.Contains(
            result.Errors,
            x => x.Key == "Curing.TakenAt");

        Assert.Contains(
            result.Errors,
            x => x.Key == "Curing.MoisturePct");

        Assert.False(
            db.CuringReading.Any(
                x => x.SpecimenId == specimen.Id));
    }

    // Verifies that adding a curing reading to a nonexistent specimen returns
    // a field-specific validation error instead of attempting to persist it.
    [Fact]
    public async Task AddReadingAsync_MissingSpecimen_ReturnsFieldError()
    {
        await using var db = _dbFixture.CreateDbContext();

        var limitProvider = new LimitProvider(db);
        var limitEvaluator = new LimitEvaluator(limitProvider);
        var repository = new CuringRepository(db);

        var service = new CuringService(
            repository,
            new CuringReadingValidator(limitEvaluator),
            new TestClock(
                new DateTime(2026, 10, 5, 18, 0, 0, DateTimeKind.Utc)));

        var result = await service.AddReadingAsync(
            999999,
            new CuringReading());

        Assert.False(result.IsSuccess);

        Assert.Contains(
            result.Errors,
            x => x.Key == "Curing.SpecimenId");
    }
}
