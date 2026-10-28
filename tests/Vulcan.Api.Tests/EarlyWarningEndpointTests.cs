using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulcan.Application.Contracts;
using Vulcan.Application.Interfaces;
using Vulcan.Application.Services;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Interfaces;
using Vulcan.Infrastructure;
using Vulcan.Infrastructure.Seed;
using Vulcan.Infrastructure.Services;

namespace Vulcan.Api.Tests;

[Collection("NonParallel")]
public class EarlyWarningEndpointTests
{
    [Fact]
    public async Task Default_request_includes_synthetic_and_real_flagged_batches_with_reasons()
    {
        var evaluator = new TestEvaluator();
        evaluator.Results["COLD"] = Evaluation(null, Reason("Curing.AmbientTempF"));
        evaluator.Results["REAL"] = Evaluation(10, Reason("Prediction.ShortfallPct"));
        await using var app = await StartAsync(evaluator);
        await AddBatchesAsync(app, BatchFor("COLD", true), BatchFor("REAL"), BatchFor("ON-TRACK"));
        using var client = ClientFor(app);

        var results = await client.GetFromJsonAsync<EarlyWarningBatchDTO[]>("/api/warnings/early");

        results.ShouldNotBeNull();
        results.Select(batch => batch.BatchNumber).ShouldBe(["REAL", "COLD"]);
        var cold = results.Single(batch => batch.BatchNumber == "COLD");
        cold.Id.ShouldBeGreaterThan(0);
        cold.IsSynthetic.ShouldBeTrue();
        cold.AtRisk.ShouldBeTrue();
        cold.Projected28DayPsi.ShouldBeNull();
        cold.ShortfallPsi.ShouldBeNull();
        cold.ShortfallPct.ShouldBeNull();
        cold.EarlyWarnings.Single().ShouldBe(Reason("Curing.AmbientTempF"));
        evaluator.Evaluated.ShouldContain("ON-TRACK");
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task Synthetic_filter_is_applied_before_evaluation(bool includeSynthetic, int expectedCount)
    {
        var evaluator = new TestEvaluator();
        evaluator.Results["SYNTHETIC"] = Evaluation(null, Reason("Curing.AmbientTempF"));
        evaluator.Results["REAL"] = Evaluation(null, Reason("Curing.AmbientTempF"));
        await using var app = await StartAsync(evaluator);
        await AddBatchesAsync(app, BatchFor("SYNTHETIC", true), BatchFor("REAL"));
        using var client = ClientFor(app);

        var results = await client.GetFromJsonAsync<EarlyWarningBatchDTO[]>(
            $"/api/warnings/early?includeSynthetic={includeSynthetic}");

        results.ShouldNotBeNull();
        results.Length.ShouldBe(expectedCount);
        evaluator.Evaluated.Count.ShouldBe(expectedCount);
        if (!includeSynthetic)
        {
            results.Single().BatchNumber.ShouldBe("REAL");
            evaluator.Evaluated.ShouldNotContain("SYNTHETIC");
        }
    }

    [Fact]
    public async Task Sorting_uses_shortfall_percentage_then_distinct_input_factors_then_batch_number()
    {
        var evaluator = new TestEvaluator();
        evaluator.Results["LARGEST-PERCENT"] = Evaluation(20, Reason("Prediction.ShortfallPct"))
            with
        { ShortfallPsi = 100 };
        evaluator.Results["TWO-FACTORS"] = Evaluation(10,
            Reason("Curing.AmbientTempF"), Reason("Curing.MoisturePct")) with
        { ShortfallPsi = 900 };
        evaluator.Results["REPEATED-READINGS"] = Evaluation(10,
            Reason("Curing.AmbientTempF"), Reason("Curing.AmbientTempF"), Reason("Curing.AmbientTempF"));
        evaluator.Results["TIE-B"] = Evaluation(5, Reason("Curing.AmbientTempF"));
        evaluator.Results["TIE-A"] = Evaluation(5, Reason("Curing.AmbientTempF"));
        evaluator.Results["NO-PREDICTION"] = Evaluation(null, Reason("Curing.AmbientTempF"));
        await using var app = await StartAsync(evaluator);
        await AddBatchesAsync(app, evaluator.Results.Keys.Select(name => BatchFor(name)).ToArray());
        using var client = ClientFor(app);

        var results = await client.GetFromJsonAsync<EarlyWarningBatchDTO[]>("/api/warnings/early");

        results.ShouldNotBeNull();
        results.Select(batch => batch.BatchNumber).ShouldBe(
            ["LARGEST-PERCENT", "TWO-FACTORS", "REPEATED-READINGS", "TIE-A", "TIE-B", "NO-PREDICTION"]);
    }

    [Fact]
    public async Task All_candidates_are_evaluated_beyond_the_search_page_size()
    {
        var evaluator = new TestEvaluator();
        evaluator.Results["BATCH-059"] = Evaluation(null, Reason("Curing.AmbientTempF"));
        await using var app = await StartAsync(evaluator);
        await AddBatchesAsync(app, Enumerable.Range(0, 60).Select(index => BatchFor($"BATCH-{index:D3}")).ToArray());
        using var client = ClientFor(app);

        var results = await client.GetFromJsonAsync<EarlyWarningBatchDTO[]>("/api/warnings/early");

        results.ShouldNotBeNull();
        results.Single().BatchNumber.ShouldBe("BATCH-059");
        evaluator.Evaluated.Count.ShouldBe(60);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task No_triggered_factors_returns_an_empty_array(bool addOnTrackBatch)
    {
        await using var app = await StartAsync(new TestEvaluator());
        if (addOnTrackBatch)
        {
            await AddBatchesAsync(app, BatchFor("ON-TRACK"));
        }
        using var client = ClientFor(app);

        var results = await client.GetFromJsonAsync<EarlyWarningBatchDTO[]>("/api/warnings/early");

        results.ShouldNotBeNull();
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task Invalid_synthetic_parameter_returns_bad_request_without_evaluating()
    {
        var evaluator = new TestEvaluator();
        await using var app = await StartAsync(evaluator);
        using var client = ClientFor(app);

        var response = await client.GetAsync("/api/warnings/early?includeSynthetic=invalid");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        evaluator.Evaluated.ShouldBeEmpty();
    }

    [Fact]
    public async Task Without_an_evaluator_the_draft_route_keeps_the_existing_api_not_found_response()
    {
        await using var app = await StartAsync(null);
        using var client = ClientFor(app);

        var response = await client.GetAsync("/api/warnings/early");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldContain("API route not found");
    }

    [Fact]
    public async Task Candidate_query_loads_the_complete_demo_graph_without_tracking_or_writes()
    {
        await using var app = await StartAsync(new TestEvaluator());
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();
        SyntheticDataSeeder.Seed(db);
        db.ChangeTracker.Clear();
        var repository = scope.ServiceProvider.GetRequiredService<IBatchRepository>();

        var batches = await repository.GetEarlyWarningCandidatesAsync(true, CancellationToken.None);

        batches.Count.ShouldBe(9);
        batches.All(batch => batch.Composition != null && batch.CementOxideAnalysis != null).ShouldBeTrue();
        batches.SelectMany(batch => batch.Specimens).Count().ShouldBe(81);
        batches.SelectMany(batch => batch.Specimens).Count(specimen => specimen.BreakResult != null).ShouldBe(81);
        batches.SelectMany(batch => batch.Specimens).SelectMany(specimen => specimen.CuringReadings).Count()
            .ShouldBe(567);
        db.ChangeTracker.Entries().ShouldBeEmpty();
        (await db.RecordWarning.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Cancelled_query_does_not_evaluate_batches()
    {
        var evaluator = new TestEvaluator();
        await using var app = await StartAsync(evaluator);
        using var scope = app.Services.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<EarlyWarningQuery>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query.GetAsync(true, cancellation.Token));

        evaluator.Evaluated.ShouldBeEmpty();
    }

    private static Batch BatchFor(string number, bool synthetic = false) => new()
    {
        BatchNumber = number,
        CastAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        IsSynthetic = synthetic
    };

    private static EarlyWarningDTO Reason(string key) => new(
        "Ambient temperature of 40 °F is below the normal minimum of 60 °F.",
        "ambientTempF", 40, key, 60);

    private static EarlyWarningEvaluation Evaluation(double? shortfall, params EarlyWarningDTO[] reasons) => new(
        true, shortfall.HasValue ? 4000 : null, shortfall.HasValue ? 500 : null, shortfall, reasons);

    private static async Task AddBatchesAsync(WebApplication app, params Batch[] batches)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();
        db.Batch.AddRange(batches);
        await db.SaveChangesAsync();
    }

    private static async Task<WebApplication> StartAsync(IEarlyWarningEvaluator? evaluator)
    {
        var app = ApiHost.Create(
            ["--environment=Testing", "--SeedSyntheticData=false", "--ConnectionStrings:Vulcan=Filename=:memory:"],
            configureServices: services =>
            {
                services.AddScoped<ICurrentTechnician, SystemTechnician>();
                if (evaluator != null)
                {
                    services.AddSingleton(evaluator);
                }
            });
        try
        {
            await app.StartAsync();
            return app;
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    private static HttpClient ClientFor(WebApplication app)
    {
        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }

    private sealed class TestEvaluator : IEarlyWarningEvaluator
    {
        public Dictionary<string, EarlyWarningEvaluation> Results { get; } = [];
        public List<string> Evaluated { get; } = [];

        public Task<EarlyWarningEvaluation> EvaluateAsync(Batch batch, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Evaluated.Add(batch.BatchNumber);
            return Task.FromResult(Results.GetValueOrDefault(batch.BatchNumber)
                ?? new EarlyWarningEvaluation(false, null, null, null, []));
        }
    }
}
