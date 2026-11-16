
using System.IO.Compression;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Vulcan.Api.Technicians;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Infrastructure;

namespace Vulcan.Api.Tests;

[Collection("NonParallel")]
public class BatchExportTests
{
    [Fact]
    public async Task Export_batch_returns_zip_with_four_csv_files()
    {
        await using var app = ApiHost.Create(["--environment=Testing"]);
        await app.StartAsync();

        using var client = ClientFor(app);

        const string batchNumber = "VM-EXPORT-001";

        // Arrange: persist a batch using the existing test database.
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<VulcanDbContext>();

            var httpContextAccessor = scope.ServiceProvider
                .GetRequiredService<IHttpContextAccessor>();

            httpContextAccessor.HttpContext = new DefaultHttpContext();
            httpContextAccessor.HttpContext.Request.Headers[
                TechnicianHeader.Name] = "Test Technician";

            db.Batch.Add(new Batch
            {
                BatchNumber = batchNumber,
                CastAt = DateTime.UtcNow.AddDays(-28),
                FinalWeightLb = 25m,
                ExpectedFinalBreakPsi = 4500,
                CementType = CementType.TypeI,
                MixWaterPh = 7.2m,
                CementSurfacePh = 7.5m,
                IsSynthetic = true
            });

            await db.SaveChangesAsync();
        }

        // Act: request the ZIP through the actual HTTP endpoint.
        using var response = await client.GetAsync(
            $"/api/batches/{batchNumber}/export");

        // Assert: successful ZIP download.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        response.Content.Headers.ContentType!
            .MediaType.ShouldBe("application/zip");

        response.Content.Headers.ContentDisposition!
            .FileName.ShouldBe($"{batchNumber}-export.zip");

        var bytes = await response.Content.ReadAsByteArrayAsync();

        bytes.Length.ShouldBeGreaterThan(0);

        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(
            stream,
            ZipArchiveMode.Read);

        var expectedFiles = new[]
        {
            "batch.csv",
            "curing-readings.csv",
            "breaks.csv",
            "predictions.csv"
        };

        archive.Entries.Count.ShouldBe(4);

        archive.Entries
            .Select(entry => entry.FullName)
            .OrderBy(name => name)
            .ShouldBe(expectedFiles.OrderBy(name => name));

        // Every CSV must contain a header row.
        foreach (var entry in archive.Entries)
        {
            using var reader = new StreamReader(entry.Open());

            var header = await reader.ReadLineAsync();

            header.ShouldNotBeNullOrWhiteSpace();
        }

        // The batch CSV must contain the requested batch.
        var batchEntry = archive.GetEntry("batch.csv");
        batchEntry.ShouldNotBeNull();

        using var batchReader = new StreamReader(batchEntry.Open());
        var batchCsv = await batchReader.ReadToEndAsync();

        batchCsv.ShouldContain(batchNumber);
        batchCsv.ShouldContain("IsSynthetic");
        batchCsv.ShouldContain("true");
    }

    [Fact]
    public async Task Export_unknown_batch_returns_not_found()
    {
        await using var app = ApiHost.Create(["--environment=Testing"]);
        await app.StartAsync();

        using var client = ClientFor(app);

        using var response = await client.GetAsync(
            "/api/batches/DOES-NOT-EXIST/export");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static HttpClient ClientFor(WebApplication app)
    {
        var address = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses.Single();

        var client = new HttpClient
        {
            BaseAddress = new Uri(address)
        };

        client.DefaultRequestHeaders.Add(
            TechnicianHeader.Name,
            "Test Technician");

        return client;
    }
    
    [Fact]
    public async Task Export_batch_preserves_all_recorded_data()
    {
        await using var app = ApiHost.Create(["--environment=Testing"]);
        await app.StartAsync();
    
        using var client = ClientFor(app);
    
        const string batchNumber = "VM-EXPORT-DATA-001";
    
        var castAt = new DateTime(
            2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    
        // Arrange: create a synthetic batch with two specimens,
        // three curing readings, and one break result.
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<VulcanDbContext>();
    
            var accessor = scope.ServiceProvider
                .GetRequiredService<IHttpContextAccessor>();
    
            accessor.HttpContext = new DefaultHttpContext();
            accessor.HttpContext.Request.Headers[
                TechnicianHeader.Name] = "Test Technician";
    
            var batch = new Batch
            {
                BatchNumber = batchNumber,
                CastAt = castAt,
                FinalWeightLb = 25m,
                ExpectedFinalBreakPsi = 4500,
                CementType = CementType.TypeI,
                MixWaterPh = 7.2m,
                CementSurfacePh = 7.5m,
                IsSynthetic = true,
    
                Specimens =
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
                            new CuringReading
                            {
                                TakenAt = castAt,
                                InternalTempF = 75.5m,
                                AmbientTempF = 70m,
                                MoisturePct = 3.2m,
                                RelativeHumidityPct = 85m,
                                ResistivityOhmPerFt = 120m,
                                Co2Ppm = 400m
                            },
                            new CuringReading
                            {
                                TakenAt = castAt.AddHours(12),
                                InternalTempF = 78.5m,
                                AmbientTempF = 72m,
                                MoisturePct = 3.5m,
                                RelativeHumidityPct = 87m,
                                ResistivityOhmPerFt = 125m,
                                Co2Ppm = 410m
                            }
                        ],
    
                        BreakResult = new BreakResult
                        {
                            BrokenAt = castAt.AddDays(3),
                            StrengthPsi = 1800
                        }
                    },
    
                    new Specimen
                    {
                        Label = "7-A",
                        LengthIn = 12m,
                        WidthIn = 6m,
                        TestAgeDays = TestAgeDays.Day7,
                        IsSynthetic = true,
    
                        CuringReadings =
                        [
                            new CuringReading
                            {
                                TakenAt = castAt.AddHours(24),
                                InternalTempF = 80m,
                                AmbientTempF = 73m,
                                MoisturePct = 3.8m,
                                RelativeHumidityPct = 89m,
                                ResistivityOhmPerFt = 130m,
                                Co2Ppm = 420m
                            }
                        ]
                    }
                ]
            };
    
            db.Batch.Add(batch);
            await db.SaveChangesAsync();
        }
    
        // Get the database's actual stored prediction count.
        // Predictions may be generated by the save interceptor.
        int expectedPredictions;
    
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<VulcanDbContext>();
    
            var batch = await db.Batch
                .SingleAsync(b => b.BatchNumber == batchNumber);
    
            expectedPredictions = await db.Prediction
                .CountAsync(p => p.BatchId == batch.Id);
        }
    
        // Act: export through the HTTP endpoint.
        using var response = await client.GetAsync(
            $"/api/batches/{batchNumber}/export");
    
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    
        using var stream = new MemoryStream(
            await response.Content.ReadAsByteArrayAsync());
    
        using var archive = new ZipArchive(
            stream, ZipArchiveMode.Read);
    
        var batchCsv = await ReadCsvAsync(archive, "batch.csv");
        var readingsCsv = await ReadCsvAsync(
            archive, "curing-readings.csv");
        var breaksCsv = await ReadCsvAsync(archive, "breaks.csv");
        var predictionsCsv = await ReadCsvAsync(
            archive, "predictions.csv");
    
        // Verify exact data-row counts.
        DataRows(batchCsv).Length.ShouldBe(1);
        DataRows(readingsCsv).Length.ShouldBe(3);
        DataRows(breaksCsv).Length.ShouldBe(1);
        DataRows(predictionsCsv).Length.ShouldBe(expectedPredictions);
    
        // Verify CSV headers and units.
        batchCsv.ShouldContain("Final weight (lb)");
        batchCsv.ShouldContain("Expected final break (PSI)");
    
        readingsCsv.ShouldContain("Internal temperature (°F)");
        readingsCsv.ShouldContain("Ambient temperature (°F)");
        readingsCsv.ShouldContain("Moisture (% of sample weight)");
        readingsCsv.ShouldContain("Resistivity (Ω/ft)");
        readingsCsv.ShouldContain("CO₂ (ppm)");
    
        breaksCsv.ShouldContain("Compressive strength (PSI)");
        predictionsCsv.ShouldContain("Estimated strength (PSI)");
    
        // Verify actual engineering values.
        readingsCsv.ShouldContain("75.5");
        readingsCsv.ShouldContain("78.5");
        readingsCsv.ShouldContain("3.2");
        readingsCsv.ShouldContain("125");
        readingsCsv.ShouldContain("410");
    
        breaksCsv.ShouldContain("3-A");
        breaksCsv.ShouldContain("1800");
        breaksCsv.ShouldNotContain("7-A");
    
        // Verify synthetic identification.
        foreach (var csv in new[]
                 {
                     batchCsv,
                     readingsCsv,
                     breaksCsv
                 })
        {
            csv.ShouldContain("IsSynthetic");
    
            foreach (var row in DataRows(csv))
            {
                row.Split(',').Last().ShouldBe("true");
            }
        }
        
        Console.WriteLine("EXPORTED CURING READINGS:");
        Console.WriteLine(readingsCsv);
    
        // Verify UTF-8-compatible timestamp formatting.
        readingsCsv.ShouldContain("2026-10-01T12:00:00");
    }
    
    [Fact]
    public async Task Export_empty_batch_produces_header_only_csv_files()
    {
        await using var app = ApiHost.Create(["--environment=Testing"]);
        await app.StartAsync();

        using var client = ClientFor(app);

        const string batchNumber = "VM-EXPORT-EMPTY-001";

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<VulcanDbContext>();

            var accessor = scope.ServiceProvider
                .GetRequiredService<IHttpContextAccessor>();

            accessor.HttpContext = new DefaultHttpContext();
            accessor.HttpContext.Request.Headers[
                TechnicianHeader.Name] = "Test Technician";

            db.Batch.Add(new Batch
            {
                BatchNumber = batchNumber,
                CastAt = DateTime.UtcNow.AddDays(-2),
                FinalWeightLb = 25m,
                ExpectedFinalBreakPsi = 4500,
                CementType = CementType.TypeI,
                MixWaterPh = 7.2m,
                IsSynthetic = false
            });

            await db.SaveChangesAsync();
        }

        using var response = await client.GetAsync(
            $"/api/batches/{batchNumber}/export");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var stream = new MemoryStream(
            await response.Content.ReadAsByteArrayAsync());

        using var archive = new ZipArchive(
            stream, ZipArchiveMode.Read);

        
        foreach (var filename in new[]
                 {
                     "curing-readings.csv",
                     "breaks.csv"
                 })
        {
            var csv = await ReadCsvAsync(archive, filename);

            csv.ShouldNotBeNullOrWhiteSpace();
            DataRows(csv).ShouldBeEmpty();
        }

        // CBAS automatically generates stored prediction records
        // when a batch is saved, even before curing data exists.
        var predictionsCsv = await ReadCsvAsync(
            archive, "predictions.csv");

        predictionsCsv.ShouldContain("IsSynthetic");

        DataRows(predictionsCsv).Length.ShouldBe(3);

        // Verify the standard prediction horizons.
        var predictionRows = DataRows(predictionsCsv);

        predictionRows.ShouldContain(row =>
            row.Contains(",3,"));

        predictionRows.ShouldContain(row =>
            row.Contains(",7,"));

        predictionRows.ShouldContain(row =>
            row.Contains(",28,"));
        
        var batchCsv = await ReadCsvAsync(archive, "batch.csv");

        DataRows(batchCsv).Length.ShouldBe(1);
        batchCsv.ShouldContain(batchNumber);
        batchCsv.ShouldContain("false");
    }
    
    [Fact]
    public async Task Export_escapes_special_characters_and_formulas()
    {
        await using var app = ApiHost.Create(["--environment=Testing"]);
        await app.StartAsync();
    
        using var client = ClientFor(app);
    
        const string batchNumber = "VM-EXPORT-ESCAPE-001";
    
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<VulcanDbContext>();
    
            var accessor = scope.ServiceProvider
                .GetRequiredService<IHttpContextAccessor>();
    
            accessor.HttpContext = new DefaultHttpContext();
            accessor.HttpContext.Request.Headers[
                TechnicianHeader.Name] = "Test Technician";
    
            db.Batch.Add(new Batch
            {
                BatchNumber = batchNumber,
                CastAt = DateTime.UtcNow.AddDays(-3),
                FinalWeightLb = 25m,
                ExpectedFinalBreakPsi = 4500,
                CementType = CementType.TypeI,
                MixWaterPh = 7.2m,
                
                Specimens =
                [
                    new Specimen
                    {
                        Label = "Cylinder, \"Special\"",
                        LengthIn = 12m,
                        WidthIn = 6m,
                        TestAgeDays = TestAgeDays.Day3,
                        CuringReadings =
                        [
                            new CuringReading
                            {
                                TakenAt = DateTime.UtcNow.AddDays(-2),
                                InternalTempF = 75m,
                                AmbientTempF = 70m,
                                MoisturePct = 3m,
                                RelativeHumidityPct = 85m,
                                ResistivityOhmPerFt = 120m,
                                Co2Ppm = 400m
                            }
                        ]
                    },
                    new Specimen
                    {
                        Label = "=SUM(1+1)",
                        LengthIn = 12m,
                        WidthIn = 6m,
                        TestAgeDays = TestAgeDays.Day7,
                        CuringReadings =
                        [
                            new CuringReading
                            {
                                TakenAt = DateTime.UtcNow.AddDays(-2),
                                InternalTempF = 77m,
                                AmbientTempF = 72m,
                                MoisturePct = 3.5m,
                                RelativeHumidityPct = 87m,
                                ResistivityOhmPerFt = 125m,
                                Co2Ppm = 410m
                            }
                        ]
                    }
                ]
            });
    
            await db.SaveChangesAsync();
        }
    
        using var response = await client.GetAsync(
            $"/api/batches/{batchNumber}/export");
    
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    
        using var stream = new MemoryStream(
            await response.Content.ReadAsByteArrayAsync());
    
        using var archive = new ZipArchive(
            stream, ZipArchiveMode.Read);
        
        var readingsCsv = await ReadCsvAsync(
            archive, "curing-readings.csv");

        var breaksCsv = await ReadCsvAsync(
            archive, "breaks.csv");

        // Both readings must be exported.
                DataRows(readingsCsv).Length.ShouldBe(2);
        
        // Commas and quotation marks must be escaped correctly.
                readingsCsv.ShouldContain(
                    "\"Cylinder, \"\"Special\"\"\"");
        
        // Formula-like values must be treated as text.
                readingsCsv.ShouldContain("'=SUM(1+1)");
        
        // Neither specimen has a break result.
        DataRows(breaksCsv).ShouldBeEmpty();
    }
    
    private static async Task<string> ReadCsvAsync(
        ZipArchive archive,
        string filename)
    {
        var entry = archive.GetEntry(filename);
        entry.ShouldNotBeNull();

        using var reader = new StreamReader(
            entry.Open(),
            Encoding.UTF8);

        return await reader.ReadToEndAsync();
    }

    private static string[] DataRows(string csv)
    {
        return csv
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .Select(row => row.TrimEnd('\r'))
            .ToArray();
    }
    
}
