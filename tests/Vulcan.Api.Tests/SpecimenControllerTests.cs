using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Vulcan.Api.Tests;

/// <summary>
/// Cylinders, breaks, readings and pending breaks through the real server (SCRUM-121).
/// Each test starts a fresh in-memory database seeded with the synthetic batches (VM-SYN-001 …).
/// </summary>
[Collection("NonParallel")]
public sealed class SpecimenControllerTests
{
    private const string _batch = "VM-SYN-001";

    [Fact]
    public async Task Adding_a_cylinder_returns_201_and_it_shows_as_pending()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var response = await AddCylinderAsync(client, "T1");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var body = await JsonAsync(response);
        var id = body.RootElement.GetProperty("id").GetInt32();
        body.RootElement.GetProperty("label").GetString().ShouldBe("T1");
        body.RootElement.GetProperty("batchNumber").GetString().ShouldBe(_batch);
        body.RootElement.GetProperty("warnings").GetArrayLength().ShouldBe(0);

        var pending = await client.GetStringAsync("/api/breaks/pending?includeSynthetic=true");
        using var list = JsonDocument.Parse(pending);
        var row = list.RootElement.EnumerateArray().Single(x => x.GetProperty("specimenId").GetInt32() == id);
        row.GetProperty("batchNumber").GetString().ShouldBe(_batch);
        row.GetProperty("testAgeDays").GetString().ShouldBe("Day28");
        row.GetProperty("isSynthetic").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Adding_a_duplicate_label_returns_400_keyed_by_label()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        (await AddCylinderAsync(client, "T1")).StatusCode.ShouldBe(HttpStatusCode.Created);
        var duplicate = await AddCylinderAsync(client, "T1");

        duplicate.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonAsync(duplicate);
        body.RootElement.GetProperty("errors").TryGetProperty("Specimen.Label", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Adding_a_cylinder_to_an_unknown_batch_returns_404()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var response = await AddCylinderAsync(client, "T1", batchNumber: "NOPE-999");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Recording_a_break_returns_201_and_a_second_break_returns_409()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);
        var id = await NewCylinderIdAsync(client);

        var first = await RecordBreakAsync(client, id, strengthPsi: 4000);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        using (var body = await JsonAsync(first))
        {
            body.RootElement.GetProperty("specimenId").GetInt32().ShouldBe(id);
            body.RootElement.GetProperty("warnings").GetArrayLength().ShouldBe(0);
        }

        var second = await RecordBreakAsync(client, id, strengthPsi: 4000);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Once broken, it's no longer pending.
        var pending = await client.GetStringAsync("/api/breaks/pending");
        using var list = JsonDocument.Parse(pending);
        list.RootElement.EnumerateArray().ShouldNotContain(x => x.GetProperty("specimenId").GetInt32() == id);
    }

    [Fact]
    public async Task A_low_break_saves_with_a_warning()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);
        var id = await NewCylinderIdAsync(client);

        var response = await RecordBreakAsync(client, id, strengthPsi: 2500);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var body = await JsonAsync(response);
        body.RootElement.GetProperty("warnings").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Recording_a_break_on_an_unknown_cylinder_returns_404()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var response = await RecordBreakAsync(client, 999999, strengthPsi: 4000);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Adding_a_reading_returns_201_and_a_bad_value_returns_400_keyed_by_field()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);
        var id = await NewCylinderIdAsync(client);

        var good = await AddReadingAsync(client, id, ambientTempF: 72);
        good.StatusCode.ShouldBe(HttpStatusCode.Created);
        using (var body = await JsonAsync(good))
        {
            body.RootElement.GetProperty("specimenId").GetInt32().ShouldBe(id);
        }

        var bad = await AddReadingAsync(client, id, ambientTempF: 720);
        bad.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var problem = await JsonAsync(bad);
        problem.RootElement.GetProperty("errors").TryGetProperty("Curing.AmbientTempF", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Adding_a_reading_with_warning_returns_201()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var id = await NewCylinderIdAsync(client);

        // 85 °F is inside the allowed 0–150 but outside the normal 60–80, so it saves with a warning.
        var response = await AddReadingAsync(
            client,
            id,
            ambientTempF: 85);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var body = await JsonAsync(response);

        var warnings = body.RootElement
            .GetProperty("warnings")
            .EnumerateArray()
            .ToArray();

        warnings.ShouldContain(w =>
            w.GetProperty("key").GetString() == "Curing.AmbientTempF");
    }

    [Fact]
    public async Task Pending_breaks_hide_synthetic_cylinders_when_asked()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);
        var id = await NewCylinderIdAsync(client);

        var pending = await client.GetStringAsync("/api/breaks/pending?includeSynthetic=false");

        // A cylinder added to a synthetic batch is synthetic too, so it's hidden.
        using var list = JsonDocument.Parse(pending);
        list.RootElement.EnumerateArray().ShouldNotContain(x => x.GetProperty("specimenId").GetInt32() == id);
    }

    [Fact]
    public async Task Pending_breaks_are_ordered_by_due_date()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        // Create cylinders out of due-date order.
        async Task<int> AddAsync(string label, string testAge)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/batches/{_batch}/specimens",
                new
                {
                    label,
                    lengthIn = 12.0,
                    widthIn = 6.0,
                    testAgeDays = testAge,
                });

            response.StatusCode.ShouldBe(HttpStatusCode.Created);

            using var body = await JsonAsync(response);
            return body.RootElement.GetProperty("id").GetInt32();
        }

        var day28 = await AddAsync("ORDER-28", "Day28");
        var day3 = await AddAsync("ORDER-3", "Day3");
        var day7 = await AddAsync("ORDER-7", "Day7");

        var response = await client.GetAsync("/api/breaks/pending?includeSynthetic=true");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var body = await JsonAsync(response);

        var ids = body.RootElement
            .EnumerateArray()
            .Select(x => x.GetProperty("specimenId").GetInt32())
            .Where(id => id == day28 || id == day3 || id == day7)
            .ToArray();

        ids.ShouldBe(new[] { day3, day7, day28 });
    }

    private static Task<HttpResponseMessage> AddCylinderAsync(
        HttpClient client,
        string label,
        string batchNumber = _batch) =>
        client.PostAsJsonAsync(
            $"/api/batches/{batchNumber}/specimens",
            new { label, lengthIn = 12.0, widthIn = 6.0, testAgeDays = "Day28" });

    private static async Task<int> NewCylinderIdAsync(HttpClient client)
    {
        var response = await AddCylinderAsync(client, "T1");
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var body = await JsonAsync(response);
        return body.RootElement.GetProperty("id").GetInt32();
    }

    private static Task<HttpResponseMessage> RecordBreakAsync(HttpClient client, int specimenId, int strengthPsi) =>
        client.PostAsJsonAsync(
            $"/api/specimens/{specimenId}/break",
            new { brokenAt = DateTime.UtcNow.AddMinutes(-5), strengthPsi });

    private static Task<HttpResponseMessage> AddReadingAsync(HttpClient client, int specimenId, double ambientTempF) =>
        client.PostAsJsonAsync(
            $"/api/specimens/{specimenId}/readings",
            new
            {
                takenAt = DateTime.UtcNow.AddMinutes(-5),
                internalTempF = 72.5,
                ambientTempF,
                moisturePct = 65.0,
                relativeHumidityPct = 55.0,
                resistivityOhmPerFt = 12.5,
                co2Ppm = 450.0,
            });

    private static async Task<JsonDocument> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task<WebApplication> StartAsync()
    {
        var app = ApiHost.Create(["--environment=Testing", "--SeedSyntheticData=true"]);
        await app.StartAsync();
        return app;
    }

    private static HttpClient ClientFor(WebApplication app)
    {
        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.Single();

        var client = new HttpClient { BaseAddress = new Uri(address) };

        // Every save needs the technician's name (RequireTechnicianOnSaves).
        client.DefaultRequestHeaders.Add("X-Technician", "Test Technician");
        return client;
    }
}
