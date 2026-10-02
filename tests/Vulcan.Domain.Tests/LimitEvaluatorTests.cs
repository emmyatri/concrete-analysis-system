using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Services;
using Vulcan.Infrastructure;
using Vulcan.Infrastructure.Services;

namespace Vulcan.Domain.Tests;

public sealed class LimitEvaluatorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<VulcanDbContext> _options;

    // ensure each test has migrations set up
    public LimitEvaluatorTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(_connection)
            .Options;
        var dbContext = new VulcanDbContext(_options);
        dbContext.Database.Migrate();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task Limit_evaluator_handles_ranges_properly()
    {
        await using (var db = new VulcanDbContext(_options))
        {
            db.ValidationLimit.AddRange(
            [
                new ValidationLimit
                {
                    Key = "Primary",
                    Severity = Severity.Warn,
                    Min = 30,
                    Message = "Don't do that"
                },
                new ValidationLimit
                {
                    Key = "Primary",
                    Severity = Severity.Reject,
                    Min = 0,
                    Max = 100,
                    Message = "Really, don't"
                }
            ]);
            await db.SaveChangesAsync();
        }

        await using (var db = new VulcanDbContext(_options))
        {
            var limitProvider = new LimitProvider(db);
            var limitEvaluator = new LimitEvaluator(limitProvider);

            limitEvaluator
                .Check("Primary", 30)
                .ShouldBeOfType<LimitResult.Ok>();
            limitEvaluator
                .Check("Primary", 29.99)
                .ShouldBeOfType<LimitResult.Warn>()
                .Message
                .ShouldBe("Don't do that");
            limitEvaluator
                .Check("Primary", -0.01)
                .ShouldBeOfType<LimitResult.Reject>()
                .Message
                .ShouldBe("Really, don't");
        }
    }

    [Fact]
    public async Task Limit_evaluator_updates_from_db()
    {
        var validationLimit = new ValidationLimit
        {
            Key = "Primary",
            Severity = Severity.Warn,
            Min = 30,
            Message = "cool"
        };
        const int testValue = 50;

        await using (var db = new VulcanDbContext(_options))
        {
            db.ValidationLimit.Add(validationLimit);
            await db.SaveChangesAsync();
        }

        await using (var db = new VulcanDbContext(_options))
        {
            var limitProvider = new LimitProvider(db);
            var limitEvaluator = new LimitEvaluator(limitProvider);

            limitEvaluator
                .Check("Primary", testValue)
                .ShouldBeOfType<LimitResult.Ok>();
        }

        // update limit
        await using (var db = new VulcanDbContext(_options))
        {
            validationLimit.Max = 30;
            db.Update(validationLimit);
            await db.SaveChangesAsync();
        }

        // same value returns a warning now
        await using (var db = new VulcanDbContext(_options))
        {
            var limitProvider = new LimitProvider(db);
            var limitEvaluator = new LimitEvaluator(limitProvider);

            limitEvaluator
                .Check("Primary", testValue)
                .ShouldBeOfType<LimitResult.Warn>()
                .Message
                .ShouldBe("cool");
        }

        // now switch it to reject and update the message
        await using (var db = new VulcanDbContext(_options))
        {
            validationLimit.Severity = Severity.Reject;
            validationLimit.Message = "really?";
            db.Update(validationLimit);
            await db.SaveChangesAsync();
        }

        await using (var db = new VulcanDbContext(_options))
        {
            var limitProvider = new LimitProvider(db);
            var limitEvaluator = new LimitEvaluator(limitProvider);

            limitEvaluator
                .Check("Primary", testValue)
                .ShouldBeOfType<LimitResult.Reject>()
                .Message
                .ShouldBe("really?");
        }
    }

    [Fact]
    public async Task Limit_evaluator_break_seed_data_is_correct()
    {
        await using (var db = new VulcanDbContext(_options))
        {
            db.SeedValidationLimits();
            await db.SaveChangesAsync();
        }

        await using (var db = new VulcanDbContext(_options))
        {
            var limitProvider = new LimitProvider(db);
            var limitEvaluator = new LimitEvaluator(limitProvider);

            limitEvaluator
                .Check(LimitKeys.StrengthPsi, 2950)
                .ShouldBeOfType<LimitResult.Warn>();
            limitEvaluator
                .Check(LimitKeys.StrengthPsi, 3000)
                .ShouldBeOfType<LimitResult.Ok>();
            limitEvaluator
                .Check(LimitKeys.StrengthPsi, 10000)
                .ShouldBeOfType<LimitResult.Ok>();
            limitEvaluator
                .Check(LimitKeys.StrengthPsi, 10000.01)
                .ShouldBeOfType<LimitResult.Reject>();
        }
    }
}
