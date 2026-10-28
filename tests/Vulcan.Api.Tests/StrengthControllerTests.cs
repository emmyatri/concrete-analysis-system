using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Vulcan.Api.Technicians;
using Vulcan.Application.Contracts;
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

    private static async Task<WebApplication> StartAsync()
    {
        var app = ApiHost.Create(["--environment=Testing"]);
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
        content.Estimates.Count.ShouldBe(3);
        content.CuringPoints.Count.ShouldBe(7);
        
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
}
