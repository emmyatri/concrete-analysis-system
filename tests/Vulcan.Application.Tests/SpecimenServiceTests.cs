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

    // Verifies that a valid specimen passes validation, is associated with the
    // requested batch, and is persisted with its identifying specimen data.
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

    // Verifies that specimen validation returns field-specific errors for all
    // invalid inputs and prevents the invalid specimen from being persisted.
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
        Assert.Equal(ServiceResultKind.Invalid, result.Kind);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, x => x.Key == "Specimen.Label");
        Assert.Contains(result.Errors, x => x.Key == "Specimen.LengthIn");
        Assert.Contains(result.Errors, x => x.Key == "Specimen.WidthIn");
        Assert.Contains(result.Errors, x => x.Key == "Specimen.TestAgeDays");

        Assert.False(
            db.Specimen.Any(x => x.BatchId == batch.Id));
    }

    // Verifies that specimen labels must be unique within a batch and that a
    // duplicate is rejected before a second specimen can be persisted.
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
        Assert.Equal(ServiceResultKind.Conflict, result.Kind);
        Assert.Contains(
            result.Errors,
            x => x.Key == "Specimen.Label");

        Assert.Equal(
            1,
            db.Specimen.Count(x => x.BatchId == batch.Id));
    }

    // Verifies that specimen labels are unique within a batch regardless of case.
    [Fact]
    public async Task AddAsync_DuplicateLabelDifferentCase_ReturnsConflict()
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
            BatchNumber = "VM-SPEC-CASE-001",
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
            Label = "A1",
            LengthIn = 12m,
            WidthIn = 6m,
            TestAgeDays = TestAgeDays.Day3
        });

        await db.SaveChangesAsync();

        var duplicate = new Specimen
        {
            Label = "a1",
            LengthIn = 12m,
            WidthIn = 6m,
            TestAgeDays = TestAgeDays.Day7
        };

        var result = await service.AddAsync(batch.Id, duplicate);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceResultKind.Conflict, result.Kind);

        Assert.Contains(
            result.Errors,
            x => x.Key == "Specimen.Label");

        Assert.Equal(
            1,
            db.Specimen.Count(x => x.BatchId == batch.Id));
    }

    // Verifies that a valid concrete break result is associated with the
    // requested specimen and persisted with its strength and break timestamp.
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

    // Verifies that a warning-level break strength still saves, returns the
    // warning to the caller, and persists it against the generated break ID.
    [Fact]
    public async Task RecordBreakAsync_WarningStrength_SavesAndPersistsWarning()
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
            BatchNumber = "VM-BREAK-WARN-001",
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
            // The 3,000 PSI warning applies to 28-day breaks only (client-decisions §3).
            TestAgeDays = TestAgeDays.Day28
        };

        db.Specimen.Add(specimen);
        await db.SaveChangesAsync();

        var breakResult = new BreakResult
        {
            BrokenAt = new DateTime(
                2026, 10, 4, 12, 0, 0, DateTimeKind.Utc),
            StrengthPsi = 2999
        };

        var result =
            await service.RecordBreakAsync(specimen.Id, breakResult);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);

        Assert.Contains(
            result.Warnings,
            warning => warning.Key == "Break.StrengthPsi");

        Assert.True(breakResult.Id > 0);

        var saved = await db.BreakResult
            .SingleOrDefaultAsync(x => x.Id == breakResult.Id);

        Assert.NotNull(saved);
        Assert.Equal(2999, saved.StrengthPsi);

        var warningRecord = Assert.Single(
            db.RecordWarning.Where(
                warning =>
                    warning.EntityType == nameof(BreakResult) &&
                    warning.EntityId == breakResult.Id &&
                    warning.LimitKey == "Break.StrengthPsi"));

        Assert.Equal(2999m, warningRecord.Value);
    }

    // Early breaks below 3,000 PSI are expected and save without a warning (client-decisions §3).
    [Fact]
    public async Task RecordBreakAsync_LowThreeDayBreak_SavesWithoutWarning()
    {
        await using var db = _dbFixture.CreateDbContext();

        var limitEvaluator = new LimitEvaluator(new LimitProvider(db));
        var service = new SpecimenService(
            new SpecimenRepository(db),
            new SpecimenValidator(limitEvaluator),
            new BreakResultValidator(limitEvaluator),
            new TestClock(new DateTime(2026, 10, 5, 18, 0, 0, DateTimeKind.Utc)));

        var batch = new Batch
        {
            BatchNumber = "VM-BREAK-EARLY-001",
            CastAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc)
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
            BrokenAt = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc),
            StrengthPsi = 2500
        };

        var result = await service.RecordBreakAsync(specimen.Id, breakResult);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Warnings);
        Assert.Empty(db.RecordWarning.Where(warning => warning.EntityId == breakResult.Id
            && warning.EntityType == nameof(BreakResult)));
    }

    // Verifies that all applicable break validation errors are returned together
    // and that an invalid break result is not persisted.
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
        Assert.Equal(ServiceResultKind.Invalid, result.Kind);
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

    // Verifies the one-break-per-specimen rule by rejecting a second break result
    // and leaving the specimen with only its original persisted result.
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
        Assert.Equal(ServiceResultKind.Conflict, result.Kind);

        Assert.Contains(
            result.Errors,
            x => x.Key == "Break.SpecimenId");

        Assert.Equal(
            1,
            db.BreakResult.Count(x => x.SpecimenId == specimen.Id));
    }

    // Verifies that recording a break for a nonexistent specimen returns a
    // field-specific validation error instead of attempting to persist the result.
    [Fact]
    public async Task RecordBreakAsync_MissingSpecimen_ReturnsFieldError()
    {
        await using var db = _dbFixture.CreateDbContext();

        var limitProvider = new LimitProvider(db);
        var limitEvaluator = new LimitEvaluator(limitProvider);
        var repository = new SpecimenRepository(db);

        var service = new SpecimenService(
            repository,
            new SpecimenValidator(limitEvaluator),
            new BreakResultValidator(limitEvaluator),
            new TestClock(
                new DateTime(2026, 10, 5, 18, 0, 0, DateTimeKind.Utc)));

        var result = await service.RecordBreakAsync(
            999999,
            new BreakResult
            {
                BrokenAt = new DateTime(
                    2026, 10, 5, 12, 0, 0, DateTimeKind.Utc),
                StrengthPsi = 5000
            });

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceResultKind.NotFound, result.Kind);

        Assert.Contains(
            result.Errors,
            x => x.Key == "Break.SpecimenId");
    }

    // Verifies that adding a specimen to a nonexistent batch returns a
    // field-specific validation error and does not create an orphaned specimen.
    [Fact]
    public async Task AddAsync_MissingBatch_ReturnsFieldErrorAndDoesNotSave()
    {
        await using var db = _dbFixture.CreateDbContext();

        var limitProvider = new LimitProvider(db);
        var limitEvaluator = new LimitEvaluator(limitProvider);
        var repository = new SpecimenRepository(db);

        var service = new SpecimenService(
            repository,
            new SpecimenValidator(limitEvaluator),
            new BreakResultValidator(limitEvaluator),
            new TestClock(
                new DateTime(2026, 10, 5, 18, 0, 0, DateTimeKind.Utc)));

        var specimen = new Specimen
        {
            Label = "C1",
            LengthIn = 12m,
            WidthIn = 6m,
            TestAgeDays = TestAgeDays.Day3
        };

        var result = await service.AddAsync(999999, specimen);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceResultKind.NotFound, result.Kind);

        Assert.Contains(
            result.Errors,
            x => x.Key == "Specimen.BatchId");

        Assert.False(
            db.Specimen.Any(x => x.BatchId == 999999));
    }
}
