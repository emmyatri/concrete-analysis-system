using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Vulcan.Domain.Constants;
using Vulcan.Infrastructure;

namespace Vulcan.Api.Tests;

[Collection("NonParallel")]
public sealed class CementSurfacePhTests
{
    private const string _batchNumber = "SYN-SURFACE-PH";
    private const string _key = LimitKeys.CementSurfacePh;

    public static IEnumerable<object[]> SurfacePhCases()
    {
        foreach (var update in new[] { false, true })
        {
            foreach (var (value, expected) in new[]
            {
                ("-0.01", "Reject"),
                ("0", "Warn"),
                ("7.09", "Warn"),
                ("7.1", "Ok"),
                ("7.5", "Ok"),
                ("8.0", "Ok"),
                ("8.01", "Warn"),
                ("8.5", "Warn"),
                ("14", "Warn"),
                ("14.01", "Reject"),
                ("7.501", "Reject"),
                ("7.500", "Ok"),
            })
            {
                yield return [update, value, expected];
            }
        }
    }

    [Theory]
    [MemberData(nameof(SurfacePhCases))]
    public async Task Create_and_update_enforce_rules_and_persist_results(
        bool update, string input, string expected)
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);
        var value = decimal.Parse(input, CultureInfo.InvariantCulture);

        if (update)
        {
            using var created = await SaveAsync(client, false, Fields(7.5m));
            created.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        using var response = await SaveAsync(client, update, Fields(value));

        if (expected == "Reject")
        {
            await AssertRejectedAsync(response);
            await AssertNoRejectedChangesAsync(app, update);
            return;
        }

        response.StatusCode.ShouldBe(
            update ? HttpStatusCode.OK : HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var warnings = body.RootElement.GetProperty("warnings").EnumerateArray().ToArray();

        if (expected == "Warn")
        {
            warnings.ShouldHaveSingleItem()
                .GetProperty("key").GetString().ShouldBe(_key);
        }
        else
        {
            warnings.ShouldBeEmpty();
        }

        using var detail = JsonDocument.Parse(
            await client.GetStringAsync($"/api/batches/{_batchNumber}"));
        detail.RootElement.GetProperty("cementSurfacePh").GetDecimal().ShouldBe(value);

        var savedWarnings = detail.RootElement.GetProperty("warnings")
            .EnumerateArray().ToArray();
        savedWarnings.Length.ShouldBe(expected == "Warn" ? 1 : 0);

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();
        var saved = await db.Batch.AsNoTracking().SingleAsync();
        saved.CementSurfacePh.ShouldBe(value);

        var records = await db.RecordWarning.AsNoTracking()
            .Where(warning => warning.LimitKey == _key).ToListAsync();
        records.Count.ShouldBe(expected == "Warn" ? 1 : 0);

        if (expected == "Warn")
        {
            var warning = records.ShouldHaveSingleItem();
            warning.EntityId.ShouldBe(saved.Id);
            warning.Value.ShouldBe(value);
            warning.Message.ShouldBe(
                "Cement surface pH is outside the normal range [7.1-8.0]");
            savedWarnings[0].GetProperty("value").GetDecimal().ShouldBe(value);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Missing_or_null_is_rejected_with_field_key(
        bool update, bool omit)
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        if (update)
        {
            using var created = await SaveAsync(client, false, Fields(7.5m));
            created.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var fields = Fields(null);
        if (omit)
        {
            fields.Remove("cementSurfacePh");
        }

        using var response = await SaveAsync(client, update, fields);

        await AssertRejectedAsync(response);
        await AssertNoRejectedChangesAsync(app, update);
    }

    [Fact]
    public async Task Historical_null_loads_and_requires_a_measurement_on_edit()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Batch
                    (BatchNumber, CastAt, FinalWeightLb, ExpectedFinalBreakPsi,
                     CementType, MixWaterPh, IsSynthetic, CreatedAtUtc, CreatedBy)
                VALUES
                    ('SYN-SURFACE-PH', '2026-10-01 12:00:00', '25', 5000,
                     'TypeI', '7.2', 1, '2026-10-01 12:00:00', 'Historical technician');
                INSERT INTO Composition
                    (BatchId, CementPct, ScmPct, ScmType, CoarseAggregatePct,
                     FineAggregatePct, WaterPct, CreatedAtUtc, CreatedBy)
                SELECT Id, '10', '0', 'None', '40', '25', '15',
                    CreatedAtUtc, CreatedBy FROM Batch;
                INSERT INTO CementOxideAnalysis
                    (BatchId, AluminaPct, IronOxidePct, MagnesiumOxidePct,
                     SulfurTrioxidePct, CreatedAtUtc, CreatedBy)
                SELECT Id, '5', '3', '2', '2', CreatedAtUtc, CreatedBy FROM Batch;
                """);
        }

        using var detail = JsonDocument.Parse(
            await client.GetStringAsync($"/api/batches/{_batchNumber}"));
        detail.RootElement.GetProperty("cementSurfacePh").ValueKind
            .ShouldBe(JsonValueKind.Null);

        var missing = Fields(null);
        missing.Remove("cementSurfacePh");
        using var rejected = await SaveAsync(client, true, missing);
        await AssertRejectedAsync(rejected);

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();
            (await db.Batch.SingleAsync()).CementSurfacePh.ShouldBeNull();
        }

        using var updated = await SaveAsync(client, true, Fields(7.5m));
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var updatedDetail = JsonDocument.Parse(
            await client.GetStringAsync($"/api/batches/{_batchNumber}"));
        updatedDetail.RootElement.GetProperty("cementSurfacePh").GetDecimal()
            .ShouldBe(7.5m);
        updatedDetail.RootElement.GetProperty("createdBy").GetString()
            .ShouldBe("Historical technician");
    }

    [Fact]
    public async Task Migration_preserves_existing_rows_and_can_be_rolled_back()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection).Options;
        await using var db = new VulcanDbContext(options);
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync("20261014194047_UpdateFinalWeightLb");
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Batch
                (BatchNumber, CastAt, FinalWeightLb, ExpectedFinalBreakPsi,
                 CementType, MixWaterPh, IsSynthetic, CreatedAtUtc, CreatedBy)
            VALUES
                ('SYN-LEGACY-PH', '2026-10-01 12:00:00', '25', 5000,
                 'TypeI', '7.2', 1, '2026-10-01 12:00:00', 'Historical technician');
            """);

        await migrator.MigrateAsync();

        db.Database.HasPendingModelChanges().ShouldBeFalse();
        var historical = await db.Batch.AsNoTracking().SingleAsync();
        historical.BatchNumber.ShouldBe("SYN-LEGACY-PH");
        historical.CementSurfacePh.ShouldBeNull();
        historical.FinalWeightLb.ShouldBe(25m);
        historical.CreatedBy.ShouldBe("Historical technician");

        await migrator.MigrateAsync("20261014194047_UpdateFinalWeightLb");

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT BatchNumber FROM Batch";
            (await command.ExecuteScalarAsync()).ShouldBe("SYN-LEGACY-PH");
            command.CommandText =
                "SELECT COUNT(*) FROM pragma_table_info('Batch') WHERE name = 'CementSurfacePh'";
            Convert.ToInt64(await command.ExecuteScalarAsync()).ShouldBe(0L);
        }

        await migrator.MigrateAsync();
        (await db.Batch.AsNoTracking().SingleAsync()).CementSurfacePh.ShouldBeNull();
    }

    private static Dictionary<string, object?> Fields(decimal? surfacePh) =>
        new()
        {
            ["batchNumber"] = _batchNumber,
            ["castAt"] = DateTime.UtcNow.AddDays(-4),
            ["finalWeightLb"] = 25,
            ["cementType"] = "TypeI",
            ["mixWaterPh"] = 7.2m,
            ["cementSurfacePh"] = surfacePh,
            ["expectedFinalBreakPsi"] = 5000,
            ["isSynthetic"] = true,
        };

    private static Task<HttpResponseMessage> SaveAsync(
        HttpClient client, bool update, Dictionary<string, object?> fields)
    {
        var composition = new
        {
            cementPct = 10,
            waterPct = 15,
            coarseAggregatePct = 40,
            fineAggregatePct = 25,
            scmType = "None",
            scmPct = 0,
        };
        var oxides = new
        {
            aluminaPct = 5,
            ironOxidePct = 3,
            magnesiumOxidePct = 2,
            sulfurTrioxidePct = 2,
        };

        if (update)
        {
            fields.Remove("batchNumber");
            fields["composition"] = composition;
            fields["oxides"] = oxides;
            return client.PutAsJsonAsync($"/api/batches/{_batchNumber}", fields);
        }

        return client.PostAsJsonAsync("/api/batches", new
        {
            batch = fields,
            composition,
            oxides,
        });
    }

    private static async Task AssertRejectedAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("errors")
            .TryGetProperty(_key, out var messages).ShouldBeTrue();
        messages.GetArrayLength().ShouldBeGreaterThan(0);
    }

    private static async Task AssertNoRejectedChangesAsync(WebApplication app, bool update)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();
        var batches = await db.Batch.AsNoTracking().ToListAsync();
        batches.Count.ShouldBe(update ? 1 : 0);
        if (update)
        {
            batches.ShouldHaveSingleItem().CementSurfacePh.ShouldBe(7.5m);
        }
        (await db.RecordWarning.CountAsync()).ShouldBe(0);
    }

    private static async Task<WebApplication> StartAsync()
    {
        var app = ApiHost.Create(
            ["--environment=Testing", "--SeedSyntheticData=false",
             "--ConnectionStrings:Vulcan=Filename=:memory:"]);
        await app.StartAsync();
        return app;
    }

    private static HttpClient ClientFor(WebApplication app)
    {
        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var client = new HttpClient { BaseAddress = new Uri(address) };
        client.DefaultRequestHeaders.Add("X-Technician", "Test Technician");
        return client;
    }
}
