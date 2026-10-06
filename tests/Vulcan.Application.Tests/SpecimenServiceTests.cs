using Microsoft.EntityFrameworkCore;
using Vulcan.Application.Interfaces;
using Vulcan.Application.Services;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Services;
using Vulcan.Infrastructure;
using Vulcan.Infrastructure.Services;

namespace Vulcan.Application.Tests;

public class SpecimenServiceTests(DatabaseFixture dbFixture)
    : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _dbFixture = dbFixture;

    [Fact]
    public async Task AddAsync_ValidSpecimen_SavesSpecimen()
    {
        await using var db = _dbFixture.CreateDbContext();

        var limitProvider = new LimitProvider(db);
        var limitEvaluator = new LimitEvaluator(limitProvider);
        var validator = new SpecimenValidator(limitEvaluator);
        var breakResultValidator = new BreakResultValidator(limitEvaluator);
        var repository = new SpecimenRepository(db);
        var clock = new TestClock(
            new DateTime(2026, 10, 5, 18, 0, 0, DateTimeKind.Utc));

        var service = new SpecimenService(
            repository,
            validator,
            breakResultValidator,
            clock);

        var batch = new Batch
        {
            BatchNumber = "VM-SPEC-001",
            CastAt = DateTime.UtcNow.AddDays(-1),
            FinalWeightLb = 30m,
            ExpectedFinalBreakPsi = 5000,
            CementType = CementType.TypeI,
            MixWaterPh = 7.2m
        };

        db.Batch.Add(batch);
        await db.SaveChangesAsync();

        var specimen = new Specimen
        {
            Label = "C1",
            LengthIn = 12m,
            WidthIn = 6m,
            TestAgeDays = TestAgeDays.Day3
        };

        var result = await service.AddAsync(batch.Id, specimen);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);

        var saved = await db.Specimen.FindAsync(specimen.Id);

        Assert.NotNull(saved);
        Assert.Equal(batch.Id, saved.BatchId);
        Assert.Equal("C1", saved.Label);
        Assert.Equal(TestAgeDays.Day3, saved.TestAgeDays);
    }

    [Fact]
    public async Task AddAsync_InvalidSpecimen_ReturnsFieldErrorsAndDoesNotSave()
    {
        await using var db = _dbFixture.CreateDbContext();

        var limitProvider = new LimitProvider(db);
        var limitEvaluator = new LimitEvaluator(limitProvider);
        var validator = new SpecimenValidator(limitEvaluator);
        var breakResultValidator = new BreakResultValidator(limitEvaluator);
        var repository = new SpecimenRepository(db);
        var clock = new TestClock(
            new DateTime(2026, 10, 5, 18, 0, 0, DateTimeKind.Utc));

        var service = new SpecimenService(
            repository,
            validator,
            breakResultValidator,
            clock);

        var batch = new Batch
        {
            BatchNumber = "VM-SPEC-002",
            CastAt = DateTime.UtcNow.AddDays(-1),
            FinalWeightLb = 30m,
            ExpectedFinalBreakPsi = 5000,
            CementType = CementType.TypeI,
            MixWaterPh = 7.2m
        };

        db.Batch.Add(batch);
        await db.SaveChangesAsync();

        var specimen = new Specimen
        {
            Label = "",
            LengthIn = 13m,
            WidthIn = 7m,
            TestAgeDays = (TestAgeDays)99
        };

        var result = await service.AddAsync(batch.Id, specimen);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, x => x.Key == "Specimen.Label");
        Assert.Contains(result.Errors, x => x.Key == "Specimen.LengthIn");
        Assert.Contains(result.Errors, x => x.Key == "Specimen.WidthIn");
        Assert.Contains(result.Errors, x => x.Key == "Specimen.TestAgeDays");

        Assert.False(
            db.Specimen.Any(x => x.BatchId == batch.Id));
    }

    [Fact]
    public async Task AddAsync_DuplicateLabel_ReturnsFieldErrorAndDoesNotSave()
    {
        await using var db = _dbFixture.CreateDbContext();

        var limitProvider = new LimitProvider(db);
        var limitEvaluator = new LimitEvaluator(limitProvider);
        var validator = new SpecimenValidator(limitEvaluator);
        var breakResultValidator = new BreakResultValidator(limitEvaluator);
        var repository = new SpecimenRepository(db);
        var clock = new TestClock(
            new DateTime(2026, 10, 5, 18, 0, 0, DateTimeKind.Utc));

        var service = new SpecimenService(
            repository,
            validator,
            breakResultValidator,
            clock);

        var batch = new Batch
        {
            BatchNumber = "VM-SPEC-003",
            CastAt = DateTime.UtcNow.AddDays(-1),
            FinalWeightLb = 30m,
            ExpectedFinalBreakPsi = 5000,
            CementType = CementType.TypeI,
            MixWaterPh = 7.2m
        };

        db.Batch.Add(batch);
        await db.SaveChangesAsync();

        db.Specimen.Add(new Specimen
        {
            BatchId = batch.Id,
            Label = "C1",
            LengthIn = 12m,
            WidthIn = 6m,
            TestAgeDays = TestAgeDays.Day3
        });

        await db.SaveChangesAsync();

        var duplicate = new Specimen
        {
            Label = "C1",
            LengthIn = 12m,
            WidthIn = 6m,
            TestAgeDays = TestAgeDays.Day7
        };

        var result = await service.AddAsync(batch.Id, duplicate);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Errors,
            x => x.Key == "Specimen.Label");

        Assert.Equal(
            1,
            db.Specimen.Count(x => x.BatchId == batch.Id));
    }

    [Fact]
    public async Task RecordBreakAsync_ValidBreak_SavesBreakResult()
    {
        await using var db = _dbFixture.CreateDbContext();

        var limitProvider = new LimitProvider(db);
        var limitEvaluator = new LimitEvaluator(limitProvider);
        var repository = new SpecimenRepository(db);

        var clock = new TestClock(
            new DateTime(2026, 10, 5, 18, 0, 0, DateTimeKind.Utc));

        var service = new SpecimenService(
            repository,
            new SpecimenValidator(limitEvaluator),
            new BreakResultValidator(limitEvaluator),
            clock);

        var batch = new Batch
        {
            BatchNumber = "VM-BREAK-001",
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
            WidthIn = 6m,
            TestAgeDays = TestAgeDays.Day3
        };

        db.Specimen.Add(specimen);
        await db.SaveChangesAsync();

        var breakResult = new BreakResult
        {
            BrokenAt = new DateTime(
                2026, 10, 4, 12, 0, 0, DateTimeKind.Utc),
            StrengthPsi = 5000
        };

        var result =
            await service.RecordBreakAsync(specimen.Id, breakResult);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);

        var saved = await db.BreakResult
            .SingleOrDefaultAsync(x => x.SpecimenId == specimen.Id);

        Assert.NotNull(saved);
        Assert.Equal(5000, saved.StrengthPsi);
        Assert.Equal(breakResult.BrokenAt, saved.BrokenAt);
    }

    [Fact]
    public async Task RecordBreakAsync_InvalidBreak_ReturnsFieldErrorsAndDoesNotSave()
    {
        await using var db = _dbFixture.CreateDbContext();

        var limitProvider = new LimitProvider(db);
        var limitEvaluator = new LimitEvaluator(limitProvider);
        var repository = new SpecimenRepository(db);

        var clock = new TestClock(
            new DateTime(2026, 10, 5, 18, 0, 0, DateTimeKind.Utc));

        var service = new SpecimenService(
            repository,
            new SpecimenValidator(limitEvaluator),
            new BreakResultValidator(limitEvaluator),
            clock);

        var batch = new Batch
        {
            BatchNumber = "VM-BREAK-002",
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
            WidthIn = 6m,
            TestAgeDays = TestAgeDays.Day3
        };

        db.Specimen.Add(specimen);
        await db.SaveChangesAsync();

        var breakResult = new BreakResult
        {
            // Invalid: before the batch was cast.
            BrokenAt = new DateTime(
                2026, 9, 30, 12, 0, 0, DateTimeKind.Utc),
            StrengthPsi = 0
        };

        var result =
            await service.RecordBreakAsync(specimen.Id, breakResult);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);

        Assert.Contains(
            result.Errors,
            x => x.Key == "Break.StrengthPsi");

        Assert.Contains(
            result.Errors,
            x => x.Key == "Break.BrokenAt");

        Assert.False(
            db.BreakResult.Any(x => x.SpecimenId == specimen.Id));
    }

    [Fact]
    public async Task RecordBreakAsync_SecondBreak_ReturnsFieldErrorAndDoesNotSave()
    {
        await using var db = _dbFixture.CreateDbContext();

        var limitProvider = new LimitProvider(db);
        var limitEvaluator = new LimitEvaluator(limitProvider);
        var repository = new SpecimenRepository(db);

        var clock = new TestClock(
            new DateTime(2026, 10, 5, 18, 0, 0, DateTimeKind.Utc));

        var service = new SpecimenService(
            repository,
            new SpecimenValidator(limitEvaluator),
            new BreakResultValidator(limitEvaluator),
            clock);

        var batch = new Batch
        {
            BatchNumber = "VM-BREAK-003",
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
            WidthIn = 6m,
            TestAgeDays = TestAgeDays.Day3
        };

        db.Specimen.Add(specimen);
        await db.SaveChangesAsync();

        db.BreakResult.Add(new BreakResult
        {
            SpecimenId = specimen.Id,
            BrokenAt = new DateTime(
                2026, 10, 4, 12, 0, 0, DateTimeKind.Utc),
            StrengthPsi = 5000
        });

        await db.SaveChangesAsync();

        var secondBreak = new BreakResult
        {
            BrokenAt = new DateTime(
                2026, 10, 5, 12, 0, 0, DateTimeKind.Utc),
            StrengthPsi = 5500
        };

        var result =
            await service.RecordBreakAsync(specimen.Id, secondBreak);

        Assert.False(result.IsSuccess);

        Assert.Contains(
            result.Errors,
            x => x.Key == "Break.SpecimenId");

        Assert.Equal(
            1,
            db.BreakResult.Count(x => x.SpecimenId == specimen.Id));
    }
}
