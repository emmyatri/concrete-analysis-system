using System.Net;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulcan.Api.Technicians;
using Vulcan.Application.Services;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Infrastructure;
using Vulcan.Infrastructure.Seed;

namespace Vulcan.Api.Tests;

/// <summary>
/// The batch export (SCRUM-213): one Excel workbook with a tab each for the batch, its curing readings,
/// its breaks and its predictions.
/// </summary>
[Collection("NonParallel")]
public class BatchExportTests
{
    private static readonly string[] _sheetNames =
    [
        BatchExportService.BatchSheet,
        BatchExportService.CuringReadingsSheet,
        BatchExportService.BreaksSheet,
        BatchExportService.PredictionsSheet
    ];

    [Fact]
    public async Task Export_returns_a_workbook_with_four_tabs()
    {
        await using var app = ApiHost.Create(["--environment=Testing"]);
        await app.StartAsync();
        using var client = ClientFor(app);

        const string batchNumber = "VM-EXPORT-001";
        await SaveAsync(app, NewBatch(batchNumber, isSynthetic: true));

        using var response = await client.GetAsync($"/api/batches/{batchNumber}/export");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe(BatchExportService.ContentType);
        response.Content.Headers.ContentDisposition!.FileName.ShouldBe($"{batchNumber}-export.xlsx");

        using var workbook = await OpenAsync(response);

        workbook.Worksheets.Select(sheet => sheet.Name).ShouldBe(_sheetNames);

        foreach (var sheet in workbook.Worksheets)
        {
            sheet.Cell(1, 1).GetString().ShouldBe("Batch number");
            LastHeader(sheet).ShouldBe("IsSynthetic");
        }

        var batchSheet = workbook.Worksheet(BatchExportService.BatchSheet);
        Cell(batchSheet, 2, "Batch number").GetString().ShouldBe(batchNumber);
        Cell(batchSheet, 2, "Expected final break (PSI)").GetDouble().ShouldBe(4500);
        Cell(batchSheet, 2, "IsSynthetic").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Export_unknown_batch_returns_not_found()
    {
        await using var app = ApiHost.Create(["--environment=Testing"]);
        await app.StartAsync();
        using var client = ClientFor(app);

        using var response = await client.GetAsync("/api/batches/DOES-NOT-EXIST/export");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Export_keeps_numbers_and_dates_as_excel_values()
    {
        await using var app = ApiHost.Create(["--environment=Testing"]);
        await app.StartAsync();
        using var client = ClientFor(app);

        const string batchNumber = "VM-EXPORT-DATA-001";
        var castAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        var batch = NewBatch(batchNumber, isSynthetic: true, castAt);
        batch.Specimens =
        [
            new Specimen
            {
                Label = "3-A",
                LengthIn = 12m,
                WidthIn = 6m,
                TestAgeDays = TestAgeDays.Day3,
                IsSynthetic = true,
                CuringReadings =
                [
                    Reading(castAt, internalTempF: 75.5m, ambientTempF: 70m),
                    Reading(castAt.AddHours(12), internalTempF: 78.5m, ambientTempF: 72m)
                ],
                BreakResult = new BreakResult { BrokenAt = castAt.AddDays(3), StrengthPsi = 1800 }
            }
        ];
        await SaveAsync(app, batch);

        using var response = await client.GetAsync($"/api/batches/{batchNumber}/export");
        using var workbook = await OpenAsync(response);

        var readings = workbook.Worksheet(BatchExportService.CuringReadingsSheet);
        DataRowCount(readings).ShouldBe(2);
        Cell(readings, 2, "Taken at (UTC)").GetDateTime().ShouldBe(castAt);
        Cell(readings, 2, "Internal temperature (°F)").GetDouble().ShouldBe(75.5);
        Cell(readings, 3, "Internal temperature (°F)").GetDouble().ShouldBe(78.5);

        var breaks = workbook.Worksheet(BatchExportService.BreaksSheet);
        DataRowCount(breaks).ShouldBe(1);
        Cell(breaks, 2, "Test age (days)").GetDouble().ShouldBe(3);
        Cell(breaks, 2, "Broken at (UTC)").GetDateTime().ShouldBe(castAt.AddDays(3));
        Cell(breaks, 2, "Compressive strength (PSI)").GetDouble().ShouldBe(1800);
    }

    [Fact]
    public async Task Export_of_a_batch_with_no_cylinders_has_header_only_tabs()
    {
        await using var app = ApiHost.Create(["--environment=Testing"]);
        await app.StartAsync();
        using var client = ClientFor(app);

        const string batchNumber = "VM-EXPORT-EMPTY-001";
        await SaveAsync(app, NewBatch(batchNumber, isSynthetic: false));

        using var response = await client.GetAsync($"/api/batches/{batchNumber}/export");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var workbook = await OpenAsync(response);

        DataRowCount(workbook.Worksheet(BatchExportService.BatchSheet)).ShouldBe(1);
        DataRowCount(workbook.Worksheet(BatchExportService.CuringReadingsSheet)).ShouldBe(0);
        DataRowCount(workbook.Worksheet(BatchExportService.BreaksSheet)).ShouldBe(0);

        // A saved batch always has its three stored predictions, even before curing data exists.
        DataRowCount(workbook.Worksheet(BatchExportService.PredictionsSheet)).ShouldBe(3);

        Cell(workbook.Worksheet(BatchExportService.BatchSheet), 2, "IsSynthetic").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Text_is_never_run_as_a_formula_and_negative_numbers_stay_numbers()
    {
        await using var app = ApiHost.Create(["--environment=Testing"]);
        await app.StartAsync();
        using var client = ClientFor(app);

        const string batchNumber = "VM-EXPORT-ESCAPE-001";
        var castAt = DateTime.UtcNow.AddDays(-3);

        var batch = NewBatch(batchNumber, isSynthetic: false, castAt);
        batch.Specimens =
        [
            new Specimen
            {
                Label = "Cylinder, \"Special\"",
                LengthIn = 12m,
                WidthIn = 6m,
                TestAgeDays = TestAgeDays.Day3,
                CuringReadings = [Reading(castAt.AddDays(1), internalTempF: 75m, ambientTempF: 70m)]
            },
            new Specimen
            {
                Label = "=SUM(1+1)",
                LengthIn = 12m,
                WidthIn = 6m,
                TestAgeDays = TestAgeDays.Day7,
                CuringReadings = [Reading(castAt.AddDays(2), internalTempF: 77m, ambientTempF: -12.5m)]
            }
        ];
        await SaveAsync(app, batch);

        using var response = await client.GetAsync($"/api/batches/{batchNumber}/export");
        using var workbook = await OpenAsync(response);

        var readings = workbook.Worksheet(BatchExportService.CuringReadingsSheet);
        DataRowCount(readings).ShouldBe(2);

        var special = Cell(readings, 2, "Specimen label");
        special.GetString().ShouldBe("Cylinder, \"Special\"");

        var formulaLike = Cell(readings, 3, "Specimen label");
        formulaLike.HasFormula.ShouldBeFalse();
        formulaLike.DataType.ShouldBe(XLDataType.Text);
        formulaLike.GetString().ShouldBe("=SUM(1+1)");

        var ambient = Cell(readings, 3, "Ambient temperature (°F)");
        ambient.DataType.ShouldBe(XLDataType.Number);
        ambient.GetDouble().ShouldBe(-12.5);
    }

    [Fact]
    public async Task Export_VM_SYN_006_includes_all_seeded_records()
    {
        await using var app = ApiHost.Create(["--environment=Testing"]);
        await app.StartAsync();
        using var client = ClientFor(app);

        const string batchNumber = "VM-SYN-006";

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();
            SetTechnician(scope);
            SyntheticDataSeeder.Seed(db);
        }

        using var response = await client.GetAsync($"/api/batches/{batchNumber}/export");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var workbook = await OpenAsync(response);

        var expectedRows = new Dictionary<string, int>
        {
            [BatchExportService.BatchSheet] = 1,
            [BatchExportService.CuringReadingsSheet] = 150, // 3 × 7 + 3 × 11 + 3 × 32
            [BatchExportService.BreaksSheet] = 9,
            [BatchExportService.PredictionsSheet] = 3
        };

        foreach (var (name, count) in expectedRows)
        {
            var sheet = workbook.Worksheet(name);
            DataRowCount(sheet).ShouldBe(count, $"Unexpected row count on {name}");

            for (var row = 2; row <= count + 1; row++)
            {
                Cell(sheet, row, "Batch number").GetString().ShouldBe(batchNumber);
                Cell(sheet, row, "IsSynthetic").GetBoolean().ShouldBeTrue($"Non-synthetic record on {name}");
            }
        }

        // Predictions keep their stored precision and creation time.
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();
            var batch = await db.Batch.SingleAsync(b => b.BatchNumber == batchNumber);
            var prediction = await db.Prediction
                .AsNoTracking()
                .Where(p => p.BatchId == batch.Id && p.EstimatedPsi != null)
                .OrderBy(p => p.HorizonDays)
                .FirstAsync();

            var predictions = workbook.Worksheet(BatchExportService.PredictionsSheet);
            var row = Enumerable.Range(2, 3)
                .First(r => Cell(predictions, r, "Horizon (days)").GetDouble() == prediction.HorizonDays);

            Cell(predictions, row, "Estimated strength (PSI)").GetDouble()
                .ShouldBe((double)prediction.EstimatedPsi!.Value, tolerance: 1e-9);
            Cell(predictions, row, "Created at (UTC)").GetDateTime()
                .ShouldBe(prediction.CreatedAtUtc, tolerance: TimeSpan.FromSeconds(1));
        }
    }

    private static Batch NewBatch(string batchNumber, bool isSynthetic, DateTime? castAt = null) => new()
    {
        BatchNumber = batchNumber,
        CastAt = castAt ?? DateTime.UtcNow.AddDays(-28),
        FinalWeightLb = 25m,
        ExpectedFinalBreakPsi = 4500,
        CementType = CementType.TypeI,
        MixWaterPh = 7.2m,
        CementSurfacePh = 7.5m,
        IsSynthetic = isSynthetic
    };

    private static CuringReading Reading(DateTime takenAt, decimal internalTempF, decimal ambientTempF) => new()
    {
        TakenAt = takenAt,
        InternalTempF = internalTempF,
        AmbientTempF = ambientTempF,
        MoisturePct = 3.2m,
        RelativeHumidityPct = 85m,
        ResistivityOhmPerFt = 120m,
        Co2Ppm = 400m
    };

    /// <summary>Saves straight through the database, with a technician for the audit fields.</summary>
    private static async Task SaveAsync(WebApplication app, Batch batch)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();
        SetTechnician(scope);
        db.Batch.Add(batch);
        await db.SaveChangesAsync();
    }

    private static void SetTechnician(IServiceScope scope)
    {
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext();
        accessor.HttpContext.Request.Headers[TechnicianHeader.Name] = "Test Technician";
    }

    private static async Task<XLWorkbook> OpenAsync(HttpResponseMessage response)
    {
        var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
        return new XLWorkbook(stream);
    }

    private static int DataRowCount(IXLWorksheet sheet) =>
        sheet.LastRowUsed()!.RowNumber() - 1;

    private static string LastHeader(IXLWorksheet sheet) =>
        sheet.Row(1).LastCellUsed()!.GetString();

    /// <summary>The cell in <paramref name="row"/> under the given header.</summary>
    private static IXLCell Cell(IXLWorksheet sheet, int row, string header)
    {
        var column = sheet.Row(1).CellsUsed()
            .First(cell => cell.GetString() == header)
            .Address.ColumnNumber;

        return sheet.Cell(row, column);
    }

    private static HttpClient ClientFor(WebApplication app)
    {
        var address = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses.Single();

        var client = new HttpClient { BaseAddress = new Uri(address) };
        client.DefaultRequestHeaders.Add(TechnicianHeader.Name, "Test Technician");
        return client;
    }
}
