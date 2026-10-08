using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vulcan.Application.Interfaces;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Interfaces;
using Vulcan.Infrastructure.Interceptors;
using Vulcan.Infrastructure.Services;

namespace Vulcan.Infrastructure.Tests;

public sealed class AuditSaveChangesInterceptorTests
{
    [Fact]
    public async Task Creating_a_record_sets_technician_and_utc_time()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var technician = new TestTechnician("Daniel");

        var options = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new AuditSaveChangesInterceptor(technician, new SystemClock()))
            .Options;

        await using var db = new VulcanDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var batch = new Batch
        {
            BatchNumber = "SYN-AUDIT-CREATE",
            IsSynthetic = true,
        };

        db.Batch.Add(batch);

        var beforeSave = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var afterSave = DateTime.UtcNow;

        db.ChangeTracker.Clear();
        var saved = await db.Batch.SingleAsync();

        saved.CreatedBy.ShouldBe("Daniel");
        saved.CreatedAtUtc.ShouldBeGreaterThanOrEqualTo(beforeSave);
        saved.CreatedAtUtc.ShouldBeLessThanOrEqualTo(afterSave);
        saved.ModifiedBy.ShouldBeNull();
        saved.ModifiedAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task Editing_a_record_preserves_creation_details_and_sets_modification_details()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var creatorOptions = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(
                new AuditSaveChangesInterceptor(
                    new TestTechnician("Daniel"), new SystemClock()))
            .Options;

        await using (var creatorDb = new VulcanDbContext(creatorOptions))
        {
            await creatorDb.Database.EnsureCreatedAsync();

            creatorDb.Batch.Add(new Batch
            {
                BatchNumber = "SYN-AUDIT-EDIT",
                IsSynthetic = true,
            });

            await creatorDb.SaveChangesAsync();
        }

        var editorOptions = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(
                new AuditSaveChangesInterceptor(
                    new TestTechnician("NotDaniel"), new SystemClock()))
            .Options;

        await using var editorDb = new VulcanDbContext(editorOptions);
        var batch = await editorDb.Batch.SingleAsync();

        var originalCreatedBy = batch.CreatedBy;
        var originalCreatedAt = batch.CreatedAtUtc;

        batch.FinalWeightLb = 30m;
        batch.CreatedBy = "Incorrect creator";
        batch.CreatedAtUtc = DateTime.MinValue;

        var beforeSave = DateTime.UtcNow;
        await editorDb.SaveChangesAsync();
        var afterSave = DateTime.UtcNow;

        editorDb.ChangeTracker.Clear();
        var saved = await editorDb.Batch.SingleAsync();

        saved.FinalWeightLb.ShouldBe(30m);
        saved.CreatedBy.ShouldBe(originalCreatedBy);
        saved.CreatedAtUtc.ShouldBe(originalCreatedAt);
        saved.ModifiedBy.ShouldBe("NotDaniel");
        saved.ModifiedAtUtc.ShouldNotBeNull();
        saved.ModifiedAtUtc.Value.ShouldBeGreaterThanOrEqualTo(beforeSave);
        saved.ModifiedAtUtc.Value.ShouldBeLessThanOrEqualTo(afterSave);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Saving_without_a_technician_rejects_the_record(string? name)
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(
                new AuditSaveChangesInterceptor(
                    new TestTechnician(name), new SystemClock()))
            .Options;

        await using var db = new VulcanDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.Batch.Add(new Batch
        {
            BatchNumber = "SYN-AUDIT-NO-TECHNICIAN",
            IsSynthetic = true,
        });

        var error = await Should.ThrowAsync<InvalidOperationException>(
            async () => await db.SaveChangesAsync());

        error.Message.ShouldContain("valid technician name");

        db.ChangeTracker.Clear();
        (await db.Batch.CountAsync()).ShouldBe(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Editing_without_a_technician_does_not_save_changes(string? name)
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var creatorOptions = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(
                new AuditSaveChangesInterceptor(
                    new TestTechnician("Daniel"), new SystemClock()))
            .Options;

        await using (var creatorDb = new VulcanDbContext(creatorOptions))
        {
            await creatorDb.Database.EnsureCreatedAsync();

            creatorDb.Batch.Add(new Batch
            {
                BatchNumber = "SYN-AUDIT-REJECT-EDIT",
                FinalWeightLb = 20m,
                IsSynthetic = true,
            });

            await creatorDb.SaveChangesAsync();
        }

        var editorOptions = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(
                new AuditSaveChangesInterceptor(
                    new TestTechnician(name), new SystemClock()))
            .Options;

        await using var editorDb = new VulcanDbContext(editorOptions);
        var batch = await editorDb.Batch.SingleAsync();

        var originalCreatedAt = batch.CreatedAtUtc;
        batch.FinalWeightLb = 30m;

        var error = await Should.ThrowAsync<InvalidOperationException>(
            async () => await editorDb.SaveChangesAsync());

        error.Message.ShouldContain("valid technician name");

        editorDb.ChangeTracker.Clear();
        var saved = await editorDb.Batch.SingleAsync();

        saved.FinalWeightLb.ShouldBe(20m);
        saved.CreatedBy.ShouldBe("Daniel");
        saved.CreatedAtUtc.ShouldBe(originalCreatedAt);
        saved.ModifiedBy.ShouldBeNull();
        saved.ModifiedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void Synchronous_saves_stamp_creation_and_modification()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var creatorOptions = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(
                new AuditSaveChangesInterceptor(
                    new TestTechnician("Daniel"), new SystemClock()))
            .Options;

        using (var creatorDb = new VulcanDbContext(creatorOptions))
        {
            creatorDb.Database.EnsureCreated();

            var batch = new Batch
            {
                BatchNumber = "SYN-AUDIT-SYNC",
                IsSynthetic = true,
            };

            creatorDb.Batch.Add(batch);

            var beforeSave = DateTime.UtcNow;
            creatorDb.SaveChanges();
            var afterSave = DateTime.UtcNow;

            creatorDb.ChangeTracker.Clear();
            var saved = creatorDb.Batch.Single();

            saved.CreatedBy.ShouldBe("Daniel");
            saved.CreatedAtUtc.ShouldBeGreaterThanOrEqualTo(beforeSave);
            saved.CreatedAtUtc.ShouldBeLessThanOrEqualTo(afterSave);
            saved.ModifiedBy.ShouldBeNull();
            saved.ModifiedAtUtc.ShouldBeNull();
        }

        var editorOptions = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(
                new AuditSaveChangesInterceptor(
                    new TestTechnician("NotDaniel"), new SystemClock()))
            .Options;

        using var editorDb = new VulcanDbContext(editorOptions);
        var existing = editorDb.Batch.Single();

        var originalCreatedAt = existing.CreatedAtUtc;
        existing.FinalWeightLb = 30m;

        var beforeEdit = DateTime.UtcNow;
        editorDb.SaveChanges();
        var afterEdit = DateTime.UtcNow;

        editorDb.ChangeTracker.Clear();
        var edited = editorDb.Batch.Single();

        edited.FinalWeightLb.ShouldBe(30m);
        edited.CreatedBy.ShouldBe("Daniel");
        edited.CreatedAtUtc.ShouldBe(originalCreatedAt);
        edited.ModifiedBy.ShouldBe("NotDaniel");
        edited.ModifiedAtUtc.ShouldNotBeNull();
        edited.ModifiedAtUtc.Value.ShouldBeGreaterThanOrEqualTo(beforeEdit);
        edited.ModifiedAtUtc.Value.ShouldBeLessThanOrEqualTo(afterEdit);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Synchronous_saves_without_a_technician_reject_creation_and_edits(
        string? name)
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var missingNameOptions = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(
                new AuditSaveChangesInterceptor(
                    new TestTechnician(name), new SystemClock()))
            .Options;

        using (var rejectedDb = new VulcanDbContext(missingNameOptions))
        {
            rejectedDb.Database.EnsureCreated();

            rejectedDb.Batch.Add(new Batch
            {
                BatchNumber = "SYN-AUDIT-SYNC-REJECT-CREATE",
                IsSynthetic = true,
            });

            var error = Should.Throw<InvalidOperationException>(
                () => rejectedDb.SaveChanges());

            error.Message.ShouldContain("valid technician name");

            rejectedDb.ChangeTracker.Clear();
            rejectedDb.Batch.Count().ShouldBe(0);
        }

        var creatorOptions = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(
                new AuditSaveChangesInterceptor(
                    new TestTechnician("Daniel"), new SystemClock()))
            .Options;

        using (var creatorDb = new VulcanDbContext(creatorOptions))
        {
            creatorDb.Batch.Add(new Batch
            {
                BatchNumber = "SYN-AUDIT-SYNC-REJECT-EDIT",
                FinalWeightLb = 20m,
                IsSynthetic = true,
            });

            creatorDb.SaveChanges();
        }

        using var editorDb = new VulcanDbContext(missingNameOptions);
        var batch = editorDb.Batch.Single();

        var originalCreatedAt = batch.CreatedAtUtc;
        batch.FinalWeightLb = 30m;

        var editError = Should.Throw<InvalidOperationException>(
            () => editorDb.SaveChanges());

        editError.Message.ShouldContain("valid technician name");

        editorDb.ChangeTracker.Clear();
        var saved = editorDb.Batch.Single();

        saved.FinalWeightLb.ShouldBe(20m);
        saved.CreatedBy.ShouldBe("Daniel");
        saved.CreatedAtUtc.ShouldBe(originalCreatedAt);
        saved.ModifiedBy.ShouldBeNull();
        saved.ModifiedAtUtc.ShouldBeNull();
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public async Task Technician_name_length_boundaries_are_enforced(
        int length, bool shouldSave)
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var name = new string('D', length);

        var options = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(
                new AuditSaveChangesInterceptor(
                    new TestTechnician(name), new SystemClock()))
            .Options;

        await using var db = new VulcanDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.Batch.Add(new Batch
        {
            BatchNumber = "SYN-AUDIT-NAME-LENGTH",
            IsSynthetic = true,
        });

        if (shouldSave)
        {
            await db.SaveChangesAsync();

            db.ChangeTracker.Clear();
            var saved = await db.Batch.SingleAsync();

            saved.CreatedBy.ShouldBe(name);
        }
        else
        {
            var error = await Should.ThrowAsync<InvalidOperationException>(
                async () => await db.SaveChangesAsync());

            error.Message.ShouldContain("valid technician name");

            db.ChangeTracker.Clear();
            (await db.Batch.CountAsync()).ShouldBe(0);
        }
    }

    [Fact]
    public async Task Related_records_receive_the_same_creation_stamp()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(
                new AuditSaveChangesInterceptor(
                    new TestTechnician("Daniel"), new SystemClock()))
            .Options;

        await using var db = new VulcanDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.Batch.Add(new Batch
        {
            BatchNumber = "SYN-AUDIT-RELATED",
            IsSynthetic = true,
            Composition = new Composition(),
            CementOxideAnalysis = new CementOxideAnalysis(),
            Specimens =
            [
                new Specimen
                {
                    Label = "SYN-CYLINDER-1",
                    IsSynthetic = true,
                },
            ],
        });

        var beforeSave = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var afterSave = DateTime.UtcNow;

        db.ChangeTracker.Clear();

        var saved = await db.Batch
            .Include(batch => batch.Composition)
            .Include(batch => batch.CementOxideAnalysis)
            .Include(batch => batch.Specimens)
            .SingleAsync();

        saved.Composition.ShouldNotBeNull();
        saved.CementOxideAnalysis.ShouldNotBeNull();
        var specimen = saved.Specimens.ShouldHaveSingleItem();

        AuditableEntity[] records =
        [
            saved,
            saved.Composition!,
            saved.CementOxideAnalysis!,
            specimen,
        ];

        foreach (var record in records)
        {
            record.CreatedBy.ShouldBe("Daniel");
            record.CreatedAtUtc.ShouldBe(saved.CreatedAtUtc);
            record.CreatedAtUtc.ShouldBeGreaterThanOrEqualTo(beforeSave);
            record.CreatedAtUtc.ShouldBeLessThanOrEqualTo(afterSave);
            record.ModifiedBy.ShouldBeNull();
            record.ModifiedAtUtc.ShouldBeNull();
        }
    }

    [Fact]
    public async Task Saving_unchanged_records_does_not_require_a_name_or_change_stamps()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var creatorOptions = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(
                new AuditSaveChangesInterceptor(
                    new TestTechnician("Daniel"), new SystemClock()))
            .Options;

        await using (var creatorDb = new VulcanDbContext(creatorOptions))
        {
            await creatorDb.Database.EnsureCreatedAsync();

            creatorDb.Batch.Add(new Batch
            {
                BatchNumber = "SYN-AUDIT-UNCHANGED",
                IsSynthetic = true,
            });

            await creatorDb.SaveChangesAsync();
        }

        var readerOptions = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(
                new AuditSaveChangesInterceptor(
                    new TestTechnician(null), new SystemClock()))
            .Options;

        await using var readerDb = new VulcanDbContext(readerOptions);
        var batch = await readerDb.Batch.SingleAsync();
        var originalCreatedAt = batch.CreatedAtUtc;

        readerDb.SaveChanges().ShouldBe(0);
        (await readerDb.SaveChangesAsync()).ShouldBe(0);

        readerDb.ChangeTracker.Clear();
        var saved = await readerDb.Batch.SingleAsync();

        saved.CreatedBy.ShouldBe("Daniel");
        saved.CreatedAtUtc.ShouldBe(originalCreatedAt);
        saved.ModifiedBy.ShouldBeNull();
        saved.ModifiedAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task Creating_a_record_preserves_prefilled_creation_details()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(
                new AuditSaveChangesInterceptor(
                    new SystemTechnician(), new SystemClock()))
            .Options;

        await using var db = new VulcanDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var castAt = new DateTime(
            2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

        db.Batch.Add(new Batch
        {
            BatchNumber = "SYN-AUDIT-PREFILLED",
            IsSynthetic = true,
            CastAt = castAt,
            CreatedBy = "Synthetic data",
            CreatedAtUtc = castAt,
        });

        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var saved = await db.Batch.SingleAsync();

        saved.CreatedBy.ShouldBe("Synthetic data");
        saved.CreatedAtUtc.ShouldBe(castAt);
        saved.ModifiedBy.ShouldBeNull();
        saved.ModifiedAtUtc.ShouldBeNull();
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task All_record_types_use_clock_for_creation_and_modification(
        bool synchronous)
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var createdAt = new DateTime(
            2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
        var modifiedAt = createdAt.AddHours(1);
        var clock = new TestClock(createdAt);
        var technician = new TestTechnician("Daniel");

        var options = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(
                new AuditSaveChangesInterceptor(technician, clock))
            .Options;

        await using var db = new VulcanDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var specimen = new Specimen
        {
            Label = "SYN-CLOCK-1",
            IsSynthetic = true,
            BreakResult = new BreakResult
            {
                BrokenAt = createdAt,
                StrengthPsi = 3000,
            },
            CuringReadings =
            [
                new CuringReading
                {
                    TakenAt = createdAt,
                    AmbientTempF = 70m,
                },
            ],
        };

        db.Batch.Add(new Batch
        {
            BatchNumber = "SYN-AUDIT-CLOCK",
            IsSynthetic = true,
            Composition = new Composition(),
            CementOxideAnalysis = new CementOxideAnalysis(),
            Specimens = [specimen],
        });

        db.RecordWarning.Add(new RecordWarning
        {
            EntityType = nameof(Batch),
            FieldName = "FinalWeightLb",
            LimitKey = "FinalWeightLb",
            Value = 20m,
            Message = "Synthetic test warning",
        });

        if (synchronous)
        {
            db.SaveChanges();
        }
        else
        {
            await db.SaveChangesAsync();
        }

        db.ChangeTracker.Clear();

        var batch = await db.Batch.SingleAsync();
        var composition = await db.Composition.SingleAsync();
        var oxides = await db.CementOxideAnalysis.SingleAsync();
        var savedSpecimen = await db.Specimen.SingleAsync();
        var breakResult = await db.BreakResult.SingleAsync();
        var reading = await db.CuringReading.SingleAsync();
        var warning = await db.RecordWarning.SingleAsync();

        AuditableEntity[] records =
        [
            batch,
            composition,
            oxides,
            savedSpecimen,
            breakResult,
            reading,
            warning,
        ];

        foreach (var record in records)
        {
            record.CreatedBy.ShouldBe("Daniel");
            record.CreatedAtUtc.ShouldBe(createdAt);
            record.ModifiedBy.ShouldBeNull();
            record.ModifiedAtUtc.ShouldBeNull();
        }

        technician.Name = "NotDaniel";
        clock.UtcNow = modifiedAt;

        batch.FinalWeightLb = 30m;
        composition.CementPct = 10m;
        oxides.AluminaPct = 5m;
        savedSpecimen.LengthIn = 12m;
        breakResult.StrengthPsi = 3500;
        reading.AmbientTempF = 75m;
        warning.Message = "Updated synthetic test warning";

        foreach (var record in records)
        {
            record.CreatedBy = "Incorrect creator";
            record.CreatedAtUtc = DateTime.MinValue;
        }

        if (synchronous)
        {
            db.SaveChanges();
        }
        else
        {
            await db.SaveChangesAsync();
        }

        db.ChangeTracker.Clear();

        AuditableEntity[] updatedRecords =
        [
            await db.Batch.SingleAsync(),
            await db.Composition.SingleAsync(),
            await db.CementOxideAnalysis.SingleAsync(),
            await db.Specimen.SingleAsync(),
            await db.BreakResult.SingleAsync(),
            await db.CuringReading.SingleAsync(),
            await db.RecordWarning.SingleAsync(),
        ];

        foreach (var record in updatedRecords)
        {
            record.CreatedBy.ShouldBe("Daniel");
            record.CreatedAtUtc.ShouldBe(createdAt);
            record.ModifiedBy.ShouldBe("NotDaniel");
            record.ModifiedAtUtc.ShouldBe(modifiedAt);
        }
    }

    private sealed class TestTechnician(string? name) : ICurrentTechnician
    {
        public string? Name { get; set; } = name;
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }
}
