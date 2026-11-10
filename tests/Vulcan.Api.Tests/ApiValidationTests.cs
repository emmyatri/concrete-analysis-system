using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Vulcan.Api.Tests;

/// <summary>
/// SCRUM-127 / M1-19:
/// Exercises server-side Reject and Warn behavior through real HTTP requests.
/// Tests start the API in the Testing environment using an in-memory SQLite database.
/// No UI validation is involved.
/// </summary>
[Collection("NonParallel")]
public sealed class ApiValidationTests
{
    private const string _batchNumber = "VM-VALIDATION-127";
    private const string _syntheticBatch = "VM-SYN-001";

    // ------------------------------------------------------------
    // Batch validation
    // ------------------------------------------------------------

    // A valid batch must be created without validation warnings.
    [Fact]
    public async Task Valid_batch_returns_201_without_warnings()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var response = await CreateBatchAsync(client);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var body = await ReadJsonAsync(response);
        body.RootElement.GetProperty("warnings")
            .GetArrayLength().ShouldBe(0);
    }

    // Reject limits must block invalid batch, composition, and oxide values with field-specific errors.
    [Theory]
    [InlineData("Batch.FinalWeightLb", 0)]
    [InlineData("Batch.ExpectedFinalBreakPsi", 2999)]
    [InlineData("Batch.MixWaterPh", 10)]
    [InlineData("Composition.CementPct", 9)]
    [InlineData("Composition.FineAggregatePct", 24)]
    [InlineData("Composition.CoarseAggregatePct", 39)]
    [InlineData("Composition.WaterPct", 21)]
    [InlineData("Composition.ScmPct", -1)]
    [InlineData("Oxide.AluminaPct", -1)]
    [InlineData("Oxide.IronOxidePct", -1)]
    [InlineData("Oxide.MagnesiumOxidePct", 7)]
    [InlineData("Oxide.SulfurTrioxidePct", 5)]
    public async Task Invalid_batch_fields_return_400_with_field_key(
        string field,
        double invalidValue)
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var response = await CreateBatchAsync(
            client,
            changes: new Dictionary<string, object?>
            {
                [field] = invalidValue
            });

        await AssertRejectedAsync(response, field);
    }

    // Batch numbers must be present, use supported characters, and fit the length limit.
    [Theory]
    [InlineData("")]
    [InlineData("INVALID@BATCH")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567")]
    public async Task Invalid_batch_numbers_return_400_with_field_key(
        string batchNumber)
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var request = ValidBatchRequest();

        var batch =
            (Dictionary<string, object?>)request["Batch"]!;

        batch["BatchNumber"] = batchNumber;

        var response = await client.PostAsJsonAsync(
            "/api/batches",
            request);

        await AssertRejectedAsync(
            response,
            "Batch.BatchNumber");
    }

    // A duplicate batch number must be rejected rather than creating another batch.
    [Fact]
    public async Task Duplicate_batch_number_returns_400_with_field_key()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var first = await CreateBatchAsync(client);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        var duplicate = await CreateBatchAsync(client);

        await AssertRejectedAsync(
            duplicate,
            "Batch.BatchNumber");
    }

    // Batch cast dates cannot be in the future.
    [Fact]
    public async Task Future_batch_cast_date_returns_400_with_field_key()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var request = ValidBatchRequest();

        var batch =
            (Dictionary<string, object?>)request["Batch"]!;

        batch["CastAt"] = DateTime.UtcNow.AddDays(1);

        var response = await client.PostAsJsonAsync(
            "/api/batches",
            request);

        await AssertRejectedAsync(
            response,
            "Batch.CastAt");
    }

    // An omitted required cast date must fail during JSON request binding.
    [Fact]
    public async Task Missing_batch_cast_date_returns_400_with_field_key()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var request = ValidBatchRequest();

        var batch =
            (Dictionary<string, object?>)request["Batch"]!;

        batch.Remove("CastAt");

        var response = await client.PostAsJsonAsync(
            "/api/batches",
            request);

        await AssertRejectedAsync(
            response,
            "$.Batch");
    }

    // Batch weight must not exceed the permitted two decimal places.
    [Fact]
    public async Task Batch_weight_with_excessive_precision_returns_400()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var request = ValidBatchRequest();

        var batch =
            (Dictionary<string, object?>)request["Batch"]!;

        batch["FinalWeightLb"] = 30.123m;

        var response = await client.PostAsJsonAsync(
            "/api/batches",
            request);

        await AssertRejectedAsync(
            response,
            "Batch.FinalWeightLb");
    }

    // Unsupported cement type values must be rejected during request binding.
    [Fact]
    public async Task Invalid_cement_type_returns_400()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var request = ValidBatchRequest();

        var batch =
            (Dictionary<string, object?>)request["Batch"]!;

        batch["CementType"] = "InvalidType";

        var response = await client.PostAsJsonAsync(
            "/api/batches",
            request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------
    // Composition validation
    // ------------------------------------------------------------

    // Combined mix percentages must not exceed 100 percent.
    [Fact]
    public async Task Composition_total_over_100_returns_400()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var response = await CreateBatchAsync(
            client,
            changes: new Dictionary<string, object?>
            {
                ["Composition.CementPct"] = 15,
                ["Composition.FineAggregatePct"] = 30,
                ["Composition.CoarseAggregatePct"] = 55,
                ["Composition.WaterPct"] = 20
            });

        await AssertRejectedAsync(response, "Composition.TotalPct");
    }

    // Each composition percentage must respect its decimal precision limit.
    [Theory]
    [InlineData("CementPct")]
    [InlineData("WaterPct")]
    [InlineData("CoarseAggregatePct")]
    [InlineData("FineAggregatePct")]
    [InlineData("ScmPct")]
    public async Task Composition_with_excessive_precision_returns_400(
        string field)
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var request = ValidBatchRequest();

        var composition =
            (Dictionary<string, object?>)request["Composition"]!;

        composition[field] = field switch
        {
            "CementPct" => 10.123m,
            "WaterPct" => 15.123m,
            "CoarseAggregatePct" => 40.123m,
            "FineAggregatePct" => 25.123m,
            "ScmPct" => 0.123m,
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

        if (field == "ScmPct")
        {
            composition["ScmType"] = "FlyAsh";
        }

        var response = await client.PostAsJsonAsync(
            "/api/batches",
            request);

        await AssertRejectedAsync(
            response,
            $"Composition.{field}");
    }

    // A positive SCM percentage requires a corresponding SCM material type.
    [Fact]
    public async Task Scm_percentage_without_type_returns_400()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var request = ValidBatchRequest();

        var composition =
            (Dictionary<string, object?>)request["Composition"]!;

        composition["ScmPct"] = 5;
        composition["ScmType"] = "None";

        var response = await client.PostAsJsonAsync(
            "/api/batches",
            request);

        await AssertRejectedAsync(
            response,
            "Composition.ScmPct");
    }

    // An unusual calculated water-to-cement ratio should warn without blocking creation.
    [Fact]
    public async Task Water_cement_ratio_outside_normal_range_warns()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var response = await CreateBatchAsync(
            client,
            changes: new Dictionary<string, object?>
            {
                ["Composition.CementPct"] = 15
            });

        await AssertWarningAsync(
            response,
            "Composition.WaterCementRatio");
    }

    // A batch without its composition must not be saved.
    [Fact]
    public async Task Missing_composition_is_rejected()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var request = ValidBatchRequest();
        request.Remove("Composition");

        var response = await client.PostAsJsonAsync(
            "/api/batches",
            request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------
    // Cement oxide validation
    // ------------------------------------------------------------

    // Each cement oxide percentage must respect its decimal precision limit.
    [Theory]
    [InlineData("AluminaPct")]
    [InlineData("IronOxidePct")]
    [InlineData("MagnesiumOxidePct")]
    [InlineData("SulfurTrioxidePct")]
    public async Task Oxide_with_excessive_precision_returns_400(
        string field)
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var request = ValidBatchRequest();

        var oxides =
            (Dictionary<string, object?>)request["Oxides"]!;

        oxides[field] = 2.123m;

        var response = await client.PostAsJsonAsync(
            "/api/batches",
            request);

        await AssertRejectedAsync(
            response,
            $"Oxide.{field}");
    }

    // Omitting an individual required oxide must produce a field-specific HTTP error.
    [Theory]
    [InlineData("AluminaPct")]
    [InlineData("IronOxidePct")]
    [InlineData("MagnesiumOxidePct")]
    [InlineData("SulfurTrioxidePct")]
    public async Task Missing_oxide_value_returns_400_with_field_key(
        string field)
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var request = ValidBatchRequest();

        var oxides =
            (Dictionary<string, object?>)request["Oxides"]!;

        oxides.Remove(field);

        var response = await client.PostAsJsonAsync(
            "/api/batches",
            request);

        await AssertRejectedAsync(
            response,
            $"Oxides.{field}");
    }

    // A batch without the oxide analysis object must not be saved.
    [Fact]
    public async Task Missing_oxides_are_rejected()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var request = ValidBatchRequest();
        request.Remove("Oxides");

        var response = await client.PostAsJsonAsync(
            "/api/batches",
            request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------
    // Batch warning behavior
    // ------------------------------------------------------------

    // Warn-tier values must save successfully and identify the affected field.
    [Theory]
    [InlineData("Batch.MixWaterPh", 8)]
    [InlineData("Oxide.AluminaPct", 10)]
    [InlineData("Oxide.IronOxidePct", 7)]
    [InlineData("Oxide.MagnesiumOxidePct", 5)]
    [InlineData("Oxide.SulfurTrioxidePct", 0.5)]
    public async Task Unusual_batch_values_save_with_field_warning(
        string field,
        double unusualValue)
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var response = await CreateBatchAsync(
            client,
            changes: new Dictionary<string, object?>
            {
                [field] = unusualValue
            });

        await AssertWarningAsync(response, field);
    }

    // ------------------------------------------------------------
    // Specimen validation
    // ------------------------------------------------------------

    // Specimen dimensions outside the permitted limits must be rejected.
    [Theory]
    [InlineData("Specimen.LengthIn", 0)]
    [InlineData("Specimen.LengthIn", 13)]
    [InlineData("Specimen.WidthIn", 0)]
    [InlineData("Specimen.WidthIn", 7)]
    public async Task Invalid_specimen_dimensions_return_400(
        string field,
        double invalidValue)
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var request = ValidSpecimenRequest();

        request[field.Split('.')[1]] = invalidValue;

        var response = await client.PostAsJsonAsync(
            $"/api/batches/{_syntheticBatch}/specimens",
            request);

        await AssertRejectedAsync(response, field);
    }

    // Specimen dimensions must not exceed their allowed decimal precision.
    [Theory]
    [InlineData("LengthIn")]
    [InlineData("WidthIn")]
    public async Task Specimen_with_excessive_precision_returns_400(
        string field)
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var request = ValidSpecimenRequest();

        request[field] = field == "LengthIn"
            ? 11.123m
            : 5.123m;

        var response = await client.PostAsJsonAsync(
            $"/api/batches/{_syntheticBatch}/specimens",
            request);

        await AssertRejectedAsync(
            response,
            $"Specimen.{field}");
    }

    // Unsupported specimen test ages must be rejected.
    [Fact]
    public async Task Invalid_specimen_test_age_returns_400()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var request = ValidSpecimenRequest();
        request["TestAgeDays"] = "InvalidAge";

        var response = await client.PostAsJsonAsync(
            $"/api/batches/{_syntheticBatch}/specimens",
            request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // An empty specimen label must fail required-field validation.
    [Fact]
    public async Task Empty_specimen_label_returns_400_with_field_key()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var request = ValidSpecimenRequest();
        request["Label"] = "";

        var response = await client.PostAsJsonAsync(
            $"/api/batches/{_syntheticBatch}/specimens",
            request);

        await AssertRejectedAsync(
            response,
            "Label");
    }

    // Specimen labels must be unique within a batch.
    [Fact]
    public async Task Duplicate_specimen_label_returns_400()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var first = await CreateSpecimenAsync(client);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        var duplicate = await CreateSpecimenAsync(client);

        await AssertRejectedAsync(
            duplicate,
            "Specimen.Label");
    }

    // ------------------------------------------------------------
    // Curing reading validation
    // ------------------------------------------------------------

    // A normal curing reading should save without warnings.
    [Fact]
    public async Task Valid_curing_reading_has_no_warnings()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var specimenId = await NewSpecimenIdAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/specimens/{specimenId}/readings",
            ValidCuringRequest());

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var body = await ReadJsonAsync(response);

        body.RootElement.GetProperty("warnings")
            .GetArrayLength().ShouldBe(0);
    }

    // Reject-tier curing measurements must produce field-specific HTTP errors.
    [Theory]
    [InlineData("Curing.InternalTempF", 201)]
    [InlineData("Curing.AmbientTempF", 151)]
    [InlineData("Curing.MoisturePct", 101)]
    [InlineData("Curing.RelativeHumidityPct", 101)]
    [InlineData("Curing.ResistivityOhmPerFt", 0)]
    [InlineData("Curing.Co2Ppm", -1)]
    public async Task Invalid_curing_values_return_400(
        string field,
        double invalidValue)
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var specimenId = await NewSpecimenIdAsync(client);
        var request = ValidCuringRequest();

        request[field.Split('.')[1]] = invalidValue;

        var response = await client.PostAsJsonAsync(
            $"/api/specimens/{specimenId}/readings",
            request);

        await AssertRejectedAsync(response, field);
    }

    // Internal curing temperature must respect its precision limit.
    [Fact]
    public async Task Internal_temperature_with_excessive_precision_returns_400()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var specimenId = await NewSpecimenIdAsync(client);
        var request = ValidCuringRequest();

        request["InternalTempF"] = 72.55m;

        var response = await client.PostAsJsonAsync(
            $"/api/specimens/{specimenId}/readings",
            request);

        await AssertRejectedAsync(
            response,
            "Curing.InternalTempF");
    }

    // Curing readings cannot be timestamped in the future.
    [Fact]
    public async Task Future_curing_reading_returns_400_with_field_key()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var specimenId = await NewSpecimenIdAsync(client);
        var request = ValidCuringRequest();

        request["TakenAt"] = DateTime.UtcNow.AddDays(1);

        var response = await client.PostAsJsonAsync(
            $"/api/specimens/{specimenId}/readings",
            request);

        await AssertRejectedAsync(
            response,
            "Curing.TakenAt");
    }

    // Curing readings cannot predate the batch cast.
    [Fact]
    public async Task Curing_reading_before_cast_date_returns_400()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var specimenId = await NewSpecimenIdAsync(client);
        var request = ValidCuringRequest();

        request["TakenAt"] = DateTime.UtcNow.AddYears(-10);

        var response = await client.PostAsJsonAsync(
            $"/api/specimens/{specimenId}/readings",
            request);

        await AssertRejectedAsync(
            response,
            "Curing.TakenAt");
    }

    // Unusual ambient temperatures should save with a field-specific warning.
    [Theory]
    [InlineData(59)]
    [InlineData(81)]
    public async Task Ambient_temperature_outside_normal_range_warns(
        double ambientTempF)
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var specimenId = await NewSpecimenIdAsync(client);
        var request = ValidCuringRequest();

        request["AmbientTempF"] = ambientTempF;

        var response = await client.PostAsJsonAsync(
            $"/api/specimens/{specimenId}/readings",
            request);

        await AssertWarningAsync(
            response,
            "Curing.AmbientTempF");
    }

    // ------------------------------------------------------------
    // Break result validation
    // ------------------------------------------------------------

    // Break strengths outside the accepted limits must be rejected.
    [Theory]
    [InlineData(0)]
    [InlineData(10001)]
    public async Task Invalid_break_strength_returns_400(
        int strengthPsi)
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var specimenId = await NewSpecimenIdAsync(client);

        var response = await RecordBreakAsync(
            client,
            specimenId,
            strengthPsi);

        await AssertRejectedAsync(
            response,
            "Break.StrengthPsi");
    }

    // Break results cannot have future timestamps.
    [Fact]
    public async Task Future_break_timestamp_returns_400_with_field_key()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var specimenId = await NewSpecimenIdAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/specimens/{specimenId}/break",
            new
            {
                BrokenAt = DateTime.UtcNow.AddDays(1),
                StrengthPsi = 4000
            });

        await AssertRejectedAsync(
            response,
            "Break.BrokenAt");
    }

    // A specimen cannot be broken before its batch was cast.
    [Fact]
    public async Task Break_before_cast_date_returns_400_with_field_key()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var specimenId = await NewSpecimenIdAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/specimens/{specimenId}/break",
            new
            {
                BrokenAt = DateTime.UtcNow.AddYears(-10),
                StrengthPsi = 4000
            });

        await AssertRejectedAsync(
            response,
            "Break.BrokenAt");
    }

    // A low but permitted break strength should save with a warning.
    [Fact]
    public async Task Low_break_strength_saves_with_warning()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var specimenId = await NewSpecimenIdAsync(client);

        var response = await RecordBreakAsync(
            client,
            specimenId,
            2500);

        await AssertWarningAsync(
            response,
            "Break.StrengthPsi");
    }

    // A specimen cannot receive a second break result.
    [Fact]
    public async Task Duplicate_break_returns_409()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var specimenId = await NewSpecimenIdAsync(client);

        var first = await RecordBreakAsync(
            client,
            specimenId,
            4000);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        var duplicate = await RecordBreakAsync(
            client,
            specimenId,
            4000);

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    // ------------------------------------------------------------
    // Request builders: valid payloads used as baselines for rejection and warning tests
    // ------------------------------------------------------------

    private static Dictionary<string, object?> ValidBatchRequest() =>
        new()
        {
            ["Batch"] = new Dictionary<string, object?>
            {
                ["BatchNumber"] = _batchNumber,
                ["CastAt"] = DateTime.UtcNow.AddDays(-4),
                ["FinalWeightLb"] = 30,
                ["CementType"] = "TypeII",
                ["MixWaterPh"] = 7.2,
                ["CementSurfacePh"] = 7.5m,
                ["ExpectedFinalBreakPsi"] = 3600,
                ["IsSynthetic"] = false
            },
            ["Composition"] = new Dictionary<string, object?>
            {
                ["CementPct"] = 10,
                ["WaterPct"] = 15,
                ["CoarseAggregatePct"] = 40,
                ["FineAggregatePct"] = 25,
                ["ScmType"] = "None",
                ["ScmPct"] = 0
            },
            ["Oxides"] = new Dictionary<string, object?>
            {
                ["IronOxidePct"] = 3.5,
                ["AluminaPct"] = 5.0,
                ["MagnesiumOxidePct"] = 2.0,
                ["SulfurTrioxidePct"] = 2.0
            }
        };

    private static async Task<HttpResponseMessage> CreateBatchAsync(
        HttpClient client,
        Dictionary<string, object?>? changes = null)
    {
        var request = ValidBatchRequest();

        if (changes is not null)
        {
            foreach (var (field, value) in changes)
            {
                var parts = field.Split('.', 2);

                var sectionName = parts[0] == "Oxide"
                    ? "Oxides"
                    : parts[0];

                var section =
                    (Dictionary<string, object?>)request[sectionName]!;

                section[parts[1]] = value;
            }
        }

        return await client.PostAsJsonAsync(
            "/api/batches",
            request);
    }

    private static Dictionary<string, object?> ValidSpecimenRequest() =>
        new()
        {
            ["Label"] = "VAL-127",
            ["LengthIn"] = 12.0,
            ["WidthIn"] = 6.0,
            ["TestAgeDays"] = "Day28"
        };

    private static Task<HttpResponseMessage> CreateSpecimenAsync(
        HttpClient client) =>
        client.PostAsJsonAsync(
            $"/api/batches/{_syntheticBatch}/specimens",
            ValidSpecimenRequest());

    private static async Task<int> NewSpecimenIdAsync(
        HttpClient client)
    {
        var response = await CreateSpecimenAsync(client);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var body = await ReadJsonAsync(response);

        return body.RootElement.GetProperty("id").GetInt32();
    }

    private static Task<HttpResponseMessage> RecordBreakAsync(
        HttpClient client,
        int specimenId,
        int strengthPsi) =>
        client.PostAsJsonAsync(
            $"/api/specimens/{specimenId}/break",
            new
            {
                BrokenAt = DateTime.UtcNow.AddMinutes(-5),
                StrengthPsi = strengthPsi
            });

    private static Dictionary<string, object?> ValidCuringRequest() =>
        new()
        {
            ["TakenAt"] = DateTime.UtcNow.AddMinutes(-5),
            ["InternalTempF"] = 72.5,
            ["AmbientTempF"] = 72.0,
            ["MoisturePct"] = 3.0,
            ["RelativeHumidityPct"] = 55.0,
            ["ResistivityOhmPerFt"] = 12.5,
            ["Co2Ppm"] = 450.0
        };

    // ------------------------------------------------------------
    // HTTP assertions: verify status codes and field-specific validation details
    // ------------------------------------------------------------

    private static async Task AssertRejectedAsync(
        HttpResponseMessage response,
        string expectedKey)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var body = await ReadJsonAsync(response);

        var errors = body.RootElement.GetProperty("errors");

        errors.TryGetProperty(expectedKey, out var messages)
            .ShouldBeTrue(
                $"Expected validation error '{expectedKey}'. " +
                $"Actual response: {body.RootElement}");

        messages.GetArrayLength().ShouldBeGreaterThan(0);
    }

    private static async Task AssertWarningAsync(
        HttpResponseMessage response,
        string expectedKey)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var body = await ReadJsonAsync(response);

        var warnings = body.RootElement
            .GetProperty("warnings")
            .EnumerateArray()
            .ToArray();

        warnings.ShouldContain(w =>
            w.GetProperty("key").GetString() == expectedKey);
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response) =>
        JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

    // ------------------------------------------------------------
    // Test server: start the real API with isolated Testing configuration
    // ------------------------------------------------------------

    private static async Task<WebApplication> StartAsync()
    {
        var app = ApiHost.Create(
            ["--environment=Testing", "--SeedSyntheticData=true"]);

        await app.StartAsync();
        return app;
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
            "X-Technician",
            "Test Technician");

        return client;
    }
}
