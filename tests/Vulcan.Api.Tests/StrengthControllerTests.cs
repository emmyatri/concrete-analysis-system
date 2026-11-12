using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Vulcan.Api.Technicians;
using Vulcan.Application.Contracts;
using Vulcan.Application.Interfaces;
using Vulcan.Application.Services;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Infrastructure;

namespace Vulcan.Api.Tests;

[Collection("NonParallel")]
public class StrengthControllerTests
{
    [Fact]
    public async Task Get_strength_unknown_batch_returns_not_found()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var response = await client.GetAsync(
            "/api/batches/DOES-NOT-EXIST/strength");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<WebApplication> StartAsync(
        Action<IServiceCollection>? configureServices = null)
    {
        var app = ApiHost.Create(
            ["--environment=Testing"],
            configureServices: configureServices);

        await app.StartAsync();
        return app;
    }

    private static HttpClient ClientFor(WebApplication app)
    {
        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.Single();

        return new HttpClient
        {
            BaseAddress = new Uri(address)
        };
    }

    [Fact]
    public async Task Get_strength_existing_batch_returns_ok()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<VulcanDbContext>();

            var httpContextAccessor = scope.ServiceProvider
                .GetRequiredService<IHttpContextAccessor>();

            httpContextAccessor.HttpContext = new DefaultHttpContext();
            httpContextAccessor.HttpContext.Request.Headers[TechnicianHeader.Name] =
                "Test Technician";

            db.Batch.Add(new Batch
            {
                BatchNumber = "VM-STRENGTH-001",
                CastAt = DateTime.UtcNow.AddDays(-4),
                FinalWeightLb = 25m,
                ExpectedFinalBreakPsi = 4500,
                CementType = CementType.TypeI,
                MixWaterPh = 7.2m,
                CementSurfacePh = 7.5m,
                IsSynthetic = false
            });

            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync(
            "/api/batches/VM-STRENGTH-001/strength");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content
            .ReadFromJsonAsync<GetBatchStrengthResponse>(
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    Converters = { new JsonStringEnumConverter() }
                });

        content.ShouldNotBeNull();
        content.BatchNumber.ShouldBe("VM-STRENGTH-001");
        content.ExpectedFinalBreakPsi.ShouldBe(4500);
        content.AccuracyTargetPct.ShouldBe(5);
        content.Status.ShouldBe("InsufficientCuringData");
        content.Estimates.Count.ShouldBe(3);
        content.Estimates.ShouldAllBe(
            estimate => estimate.EstimatedPsi == null);

        content.CurvePoints.ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_strength_with_curing_readings_returns_28_point_curve()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var castAt = DateTime.UtcNow.AddDays(-4);

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<VulcanDbContext>();

            var httpContextAccessor = scope.ServiceProvider
                .GetRequiredService<IHttpContextAccessor>();

            httpContextAccessor.HttpContext = new DefaultHttpContext();
            httpContextAccessor.HttpContext.Request.Headers[TechnicianHeader.Name] =
                "Test Technician";

            var batch = new Batch
            {
                BatchNumber = "VM-STRENGTH-002",
                CastAt = castAt,
                FinalWeightLb = 25m,
                ExpectedFinalBreakPsi = 4500,
                CementType = CementType.TypeI,
                MixWaterPh = 7.2m,
                CementSurfacePh = 7.5m,
                IsSynthetic = false
            };

            db.Batch.Add(batch);
            await db.SaveChangesAsync();

            var specimen = new Specimen
            {
                BatchId = batch.Id,
                Label = "D28-1",
                LengthIn = 12m,
                WidthIn = 6m,
                TestAgeDays = TestAgeDays.Day28,
                IsSynthetic = false
            };

            db.Specimen.Add(specimen);
            await db.SaveChangesAsync();

            for (var i = 0; i <= 6; i++)
            {
                db.CuringReading.Add(new CuringReading
                {
                    SpecimenId = specimen.Id,
                    TakenAt = castAt.AddHours(i * 12),
                    InternalTempF = 73.4m,
                    AmbientTempF = 73.4m,
                    MoisturePct = 50m,
                    RelativeHumidityPct = 60m,
                    ResistivityOhmPerFt = 100m,
                    Co2Ppm = 400m
                });
            }

            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync(
            "/api/batches/VM-STRENGTH-002/strength");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content
            .ReadFromJsonAsync<GetBatchStrengthResponse>(
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    Converters = { new JsonStringEnumConverter() }
                });

        content.ShouldNotBeNull();
        content.BatchNumber.ShouldBe("VM-STRENGTH-002");
        content.ExpectedFinalBreakPsi.ShouldBe(4500);

        content.CurvePoints.Count.ShouldBe(28);
        content.UnadjustedCurvePoints.ShouldBeEmpty();
        content.Estimates.Count.ShouldBe(3);
        content.CuringPoints.Count.ShouldBe(7);

        var curingPoints = content.CuringPoints
            .OrderBy(point => point.Time)
            .ToList();

        // Verify all readings belong to the correct specimen.
        curingPoints.ShouldAllBe(
            point => point.SpecimenLabel == "D28-1");

        // Verify the timestamps match the stored readings.
        for (var i = 0; i <= 6; i++)
        {
            curingPoints[i].Time.ShouldBe(castAt.AddHours(i * 12));
        }

        // Verify the recorded curing measurements.
        curingPoints.ShouldAllBe(point =>
            point.InternalTempF == 73.4 &&
            point.AmbientTempF == 73.4 &&
            point.MoisturePct == 50 &&
            point.RelativeHumidityPct == 60 &&
            point.ResistivityOhmPerFt == 100 &&
            point.Co2Ppm == 400);

        // Verify maturity increases as curing progresses.
        curingPoints[0].MaturityFHours.ShouldBe(0);

        for (var i = 1; i < curingPoints.Count; i++)
        {
            curingPoints[i].MaturityFHours.ShouldNotBeNull();

            curingPoints[i].MaturityFHours!.Value.ShouldBeGreaterThan(
                curingPoints[i - 1].MaturityFHours!.Value);
        }

        content.CurvePoints.ShouldAllBe(point => point.Psi > 0);
        content.CurvePoints[0].Day.ShouldBe(1);
        content.CurvePoints[^1].Day.ShouldBe(28);

        content.Estimates.Select(e => e.HorizonDays)
            .ShouldBe(new[] { 3, 7, 28 });

        content.IsSynthetic.ShouldBeFalse();

        content.Estimates.ShouldAllBe(
            estimate => estimate.IsSynthetic == false);
    }

    [Fact]
    public async Task Get_strength_synthetic_batch_preserves_synthetic_flag()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var castAt = DateTime.UtcNow.AddDays(-4);

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<VulcanDbContext>();

            var httpContextAccessor = scope.ServiceProvider
                .GetRequiredService<IHttpContextAccessor>();

            httpContextAccessor.HttpContext = new DefaultHttpContext();
            httpContextAccessor.HttpContext.Request.Headers[TechnicianHeader.Name] =
                "Test Technician";

            var batch = new Batch
            {
                BatchNumber = "VM-STRENGTH-SYN-001",
                CastAt = castAt,
                FinalWeightLb = 25m,
                ExpectedFinalBreakPsi = 4500,
                CementType = CementType.TypeI,
                MixWaterPh = 7.2m,
                CementSurfacePh = 7.5m,
                IsSynthetic = true
            };

            db.Batch.Add(batch);
            await db.SaveChangesAsync();

            var specimen = new Specimen
            {
                BatchId = batch.Id,
                Label = "D28-1",
                LengthIn = 12m,
                WidthIn = 6m,
                TestAgeDays = TestAgeDays.Day28,
                IsSynthetic = true
            };

            db.Specimen.Add(specimen);
            await db.SaveChangesAsync();

            for (var i = 0; i <= 6; i++)
            {
                db.CuringReading.Add(new CuringReading
                {
                    SpecimenId = specimen.Id,
                    TakenAt = castAt.AddHours(i * 12),
                    InternalTempF = 73.4m,
                    AmbientTempF = 73.4m,
                    MoisturePct = 50m,
                    RelativeHumidityPct = 60m,
                    ResistivityOhmPerFt = 100m,
                    Co2Ppm = 400m
                });
            }

            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync(
            "/api/batches/VM-STRENGTH-SYN-001/strength");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content
            .ReadFromJsonAsync<GetBatchStrengthResponse>(
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    Converters = { new JsonStringEnumConverter() }
                });

        content.ShouldNotBeNull();
        content.IsSynthetic.ShouldBeTrue();
        content.CuringPoints.Count.ShouldBe(7);
    }

    [Fact]
    public async Task Get_strength_returns_stored_prediction_values()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<VulcanDbContext>();

            var httpContextAccessor = scope.ServiceProvider
                .GetRequiredService<IHttpContextAccessor>();

            httpContextAccessor.HttpContext = new DefaultHttpContext();
            httpContextAccessor.HttpContext.Request.Headers[TechnicianHeader.Name] =
                "Test Technician";

            var calibrationVersion = db.Set<CalibrationVersion>()
                .First();

            var batch = new Batch
            {
                BatchNumber = "VM-STRENGTH-003",
                CastAt = DateTime.UtcNow.AddDays(-4),
                FinalWeightLb = 25m,
                ExpectedFinalBreakPsi = 4500,
                CementType = CementType.TypeI,
                MixWaterPh = 7.2m,
                CementSurfacePh = 7.5m,
                IsSynthetic = false
            };

            db.Batch.Add(batch);
            await db.SaveChangesAsync();

            var predictions = await db.Set<PredictionEntity>()
                .Where(p => p.BatchId == batch.Id)
                .ToListAsync();

            predictions.Count.ShouldBe(3);

            foreach (var prediction in predictions)
            {
                prediction.EstimatedPsi = prediction.HorizonDays switch
                {
                    3 => 2500m,
                    7 => 3500m,
                    28 => 4500m,
                    _ => throw new InvalidOperationException(
                        "Unexpected prediction horizon.")
                };

                prediction.Basis = "Maturity";
                prediction.CalibrationVersion = calibrationVersion.Version;
            }

            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync(
            "/api/batches/VM-STRENGTH-003/strength");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content
            .ReadFromJsonAsync<GetBatchStrengthResponse>(
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    Converters = { new JsonStringEnumConverter() }
                });

        content.ShouldNotBeNull();

        foreach (var estimate in content.Estimates)
        {
            Console.WriteLine(
                $"Day: {estimate.HorizonDays}, " +
                $"PSI: {estimate.EstimatedPsi}, " +
                $"Basis: {estimate.Basis}");
        }

        content.Estimates.Count.ShouldBe(3);

        content.Estimates[0].HorizonDays.ShouldBe(3);
        content.Estimates[0].EstimatedPsi.ShouldBe(2500);

        content.Estimates[1].HorizonDays.ShouldBe(7);
        content.Estimates[1].EstimatedPsi.ShouldBe(3500);

        content.Estimates[2].HorizonDays.ShouldBe(28);
        content.Estimates[2].EstimatedPsi.ShouldBe(4500);

        content.Projected28DayPsi.ShouldBe(4500);
        content.Status.ShouldBe("Estimated");
    }

    [Fact]
    public async Task Get_strength_returns_measured_break_with_synthetic_flag()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var castAt = DateTime.UtcNow.AddDays(-4);

        int specimenId;

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<VulcanDbContext>();

            var httpContextAccessor = scope.ServiceProvider
                .GetRequiredService<IHttpContextAccessor>();

            httpContextAccessor.HttpContext = new DefaultHttpContext();
            httpContextAccessor.HttpContext.Request.Headers[TechnicianHeader.Name] =
                "Test Technician";

            var batch = new Batch
            {
                BatchNumber = "VM-STRENGTH-BREAK-001",
                CastAt = castAt,
                FinalWeightLb = 25m,
                ExpectedFinalBreakPsi = 4500,
                CementType = CementType.TypeI,
                MixWaterPh = 7.2m,
                CementSurfacePh = 7.5m,
                IsSynthetic = true
            };

            db.Batch.Add(batch);
            await db.SaveChangesAsync();

            var specimen = new Specimen
            {
                BatchId = batch.Id,
                Label = "D3-1",
                LengthIn = 12m,
                WidthIn = 6m,
                TestAgeDays = TestAgeDays.Day3,
                IsSynthetic = true
            };

            db.Specimen.Add(specimen);
            await db.SaveChangesAsync();

            specimenId = specimen.Id;

            db.Set<BreakResult>().Add(new BreakResult
            {
                SpecimenId = specimen.Id,
                BrokenAt = castAt.AddDays(3),
                StrengthPsi = 2800
            });

            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync(
            "/api/batches/VM-STRENGTH-BREAK-001/strength");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content
            .ReadFromJsonAsync<GetBatchStrengthResponse>(
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    Converters = { new JsonStringEnumConverter() }
                });

        content.ShouldNotBeNull();
        content.MeasuredBreaks.Count.ShouldBe(1);

        var measuredBreak = content.MeasuredBreaks.Single();

        measuredBreak.SpecimenId.ShouldBe(specimenId);
        measuredBreak.SpecimenLabel.ShouldBe("D3-1");
        measuredBreak.AgeDays.ShouldBe(3);
        measuredBreak.Psi.ShouldBe(2800);
        measuredBreak.IsSynthetic.ShouldBeTrue();
    }

    [Fact]
    public async Task Get_strength_returns_early_warning_evaluation()
    {
        await using var app = await StartAsync(services =>
        {
            services.AddScoped<IEarlyWarningEvaluator, TestEarlyWarningEvaluator>();
        });

        using var client = ClientFor(app);

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<VulcanDbContext>();

            var httpContextAccessor = scope.ServiceProvider
                .GetRequiredService<IHttpContextAccessor>();

            httpContextAccessor.HttpContext = new DefaultHttpContext();
            httpContextAccessor.HttpContext.Request.Headers[TechnicianHeader.Name] =
                "Test Technician";

            db.Batch.Add(new Batch
            {
                BatchNumber = "VM-STRENGTH-WARNING-001",
                CastAt = DateTime.UtcNow.AddDays(-4),
                FinalWeightLb = 25m,
                ExpectedFinalBreakPsi = 4500,
                CementType = CementType.TypeI,
                MixWaterPh = 7.2m,
                CementSurfacePh = 7.5m,
                IsSynthetic = false
            });

            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync(
            "/api/batches/VM-STRENGTH-WARNING-001/strength");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content
            .ReadFromJsonAsync<GetBatchStrengthResponse>(
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    Converters = { new JsonStringEnumConverter() }
                });

        content.ShouldNotBeNull();

        content.Status.ShouldBe("Estimated");
        content.AtRisk.ShouldBeTrue();
        content.Projected28DayPsi.ShouldBe(3800);
        content.ShortfallPsi.ShouldBe(700);
        content.ShortfallPct.ShouldBe(15.56);

        content.EarlyWarnings.Count.ShouldBe(1);

        var warning = content.EarlyWarnings.Single();

        warning.Warning.ShouldBe("Low projected strength");
        warning.Field.ShouldBe("ExpectedFinalBreakPsi");
        warning.Value.ShouldBe(3800);
        warning.LimitKey.ShouldBe("Prediction.ShortfallPct");
        warning.Limit.ShouldBe(5.0);
    }

    private sealed class TestEarlyWarningEvaluator : IEarlyWarningEvaluator
    {
        public Task<EarlyWarningEvaluation> EvaluateAsync(
            Batch batch,
            CancellationToken cancellationToken)
        {
            var result = new EarlyWarningEvaluation(
                AtRisk: true,
                Projected28DayPsi: 3800,
                ShortfallPsi: 700,
                ShortfallPct: 15.56,
                EarlyWarnings:
                [
                    new EarlyWarningDTO(
                        Warning: "Low projected strength",
                        Field: "ExpectedFinalBreakPsi",
                        Value: 3800,
                        LimitKey: "Prediction.ShortfallPct",
                        Limit: 5.0)
                ]);

            return Task.FromResult(result);
        }
    }
}
