using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Vulcan.Api.Technicians;
using Vulcan.Application.Contracts;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Infrastructure;

namespace Vulcan.Api.Tests;

[Collection("NonParallel")]
public class BatchControllerTests()
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter(),
        }
    };

    // --- POST /api/batches
    [Fact]
    public async Task Create_valid_batch_works()
    {
        await using var app = await StartAsync();
        var client = ClientFor(app);

        var response = await client.PostAsJsonAsync("/api/batches", new
        {
            Batch = new
            {
                BatchNumber = "VMDE-355",
                CastAt = DateTime.UtcNow.AddDays(-4),
                FinalWeightLb = 30,
                CementType = Domain.Enums.CementType.TypeII,
                MixWaterPh = 7.0,
                ExpectedFinalBreakPsi = 3600,
                IsSynthetic = false
            },
            Oxides = new
            {
                IronOxidePct = 3.5,
                AluminaPct = 13.53,
                MagnesiumOxidePct = 5.5,
                SulfurTrioxidePct = 0.2
            },
            Composition = new
            {
                CementPct = 10,
                WaterPct = 15,
                CoarseAggregatePct = 40,
                FineAggregatePct = 25,
                ScmType = "FlyAsh",
                ScmPct = 10,
            }
        });

        response
            .StatusCode
            .ShouldBe(System.Net.HttpStatusCode.Created);

        var content = await response
            .Content
            .ReadFromJsonAsync<CreateBatchResponse>(_jsonOptions);
        content
            .ShouldNotBeNull()
            .ShouldSatisfyAllConditions(
                it => it.BatchNumber.ShouldBe("VMDE-355"),
                it => it.Id.ShouldBeGreaterThan(0)
            );
    }

    [Fact]
    public async Task Create_conflict_batch_fails()
    {
        await using var app = await StartAsync();
        var client = ClientFor(app);

        await client.PostAsJsonAsync("/api/batches", new
        {
            Batch = new
            {
                BatchNumber = "VMDE-355",
                CastAt = DateTime.UtcNow.AddDays(-4),
                FinalWeightLb = 30,
                CementType = Domain.Enums.CementType.TypeII,
                MixWaterPh = 7.0,
                ExpectedFinalBreakPsi = 3600,
                IsSynthetic = false
            },
            Oxides = new
            {
                IronOxidePct = 3.5,
                AluminaPct = 13.53,
                MagnesiumOxidePct = 5.5,
                SulfurTrioxidePct = 0.2
            },
            Composition = new
            {
                CementPct = 10,
                WaterPct = 15,
                CoarseAggregatePct = 40,
                FineAggregatePct = 25,
                ScmType = "FlyAsh",
                ScmPct = 10,
            }
        });

        var response = await client.PostAsJsonAsync("/api/batches", new
        {
            Batch = new
            {
                BatchNumber = "VMDE-355",
                CastAt = DateTime.UtcNow.AddDays(-4),
                FinalWeightLb = 30,
                CementType = Domain.Enums.CementType.TypeII,
                MixWaterPh = 7.0,
                ExpectedFinalBreakPsi = 3600,
                IsSynthetic = false
            },
            Oxides = new
            {
                IronOxidePct = 3.5,
                AluminaPct = 13.53,
                MagnesiumOxidePct = 5.5,
                SulfurTrioxidePct = 0.2
            },
            Composition = new
            {
                CementPct = 10,
                WaterPct = 15,
                CoarseAggregatePct = 40,
                FineAggregatePct = 25,
                ScmType = "FlyAsh",
                ScmPct = 10,
            }
        });

        response
            .StatusCode
            .ShouldBe(System.Net.HttpStatusCode.BadRequest);

        var content = await response
            .Content
            .ReadFromJsonAsync<ValidationProblemDetails>(_jsonOptions);
        content
            .ShouldNotBeNull()
            .ShouldSatisfyAllConditions(
                it => it.Errors.FirstOrDefault().Key.ShouldBe("Batch.BatchNumber"),
                it => it.Errors.FirstOrDefault().Value.First().ShouldBe("Batch number already exists.")
            );
    }

    [Fact]
    public async Task Create_invalid_batch_fails()
    {
        await using var app = await StartAsync();
        var client = ClientFor(app);

        var response = await client.PostAsJsonAsync("/api/batches", new
        {
            Batch = new
            {
                BatchNumber = "VMDE-355",
                CastAt = DateTime.UtcNow.AddDays(-4),
                FinalWeightLb = 30,
                CementType = Domain.Enums.CementType.TypeII,
                MixWaterPh = 7.0,
                ExpectedFinalBreakPsi = 3600,
                IsSynthetic = false
            },
            Oxides = new
            {
                IronOxidePct = 3.5,
                AluminaPct = -209, // wrong
                MagnesiumOxidePct = 5.5,
                SulfurTrioxidePct = 0.2
            },
            Composition = new
            {
                CementPct = 10,
                WaterPct = 1500, // wrong
                CoarseAggregatePct = 40,
                FineAggregatePct = 25,
                ScmType = "None",
                ScmPct = 10, // wrong for type
            }
        });

        response
            .StatusCode
            .ShouldBe(System.Net.HttpStatusCode.BadRequest);

        var content = await response
            .Content
            .ReadFromJsonAsync<ValidationProblemDetails>(_jsonOptions);
        content
            .ShouldNotBeNull()
            .Errors
            .Keys
            .ShouldSatisfyAllConditions(
                it => it.ShouldContain("Oxide.AluminaPct"),
                it => it.ShouldContain("Composition.TotalPct"),
                it => it.ShouldContain("Composition.WaterPct"),
                it => it.ShouldContain("Composition.ScmPct"),
                it => it.Count.ShouldBe(4)
            );
    }

    // PUT /api/batches/{number}
    [Fact]
    public async Task Update_batch_works()
    {
        await using var app = await StartAsync();
        var client = ClientFor(app);

        await client.PostAsJsonAsync("/api/batches", new
        {
            Batch = new
            {
                BatchNumber = "VMDE-355",
                CastAt = DateTime.UtcNow.AddDays(-4),
                FinalWeightLb = 30,
                CementType = Domain.Enums.CementType.TypeII,
                MixWaterPh = 7.0,
                ExpectedFinalBreakPsi = 3600,
                IsSynthetic = false
            },
            Oxides = new
            {
                IronOxidePct = 3.5,
                AluminaPct = 13.53,
                MagnesiumOxidePct = 5.5,
                SulfurTrioxidePct = 0.2
            },
            Composition = new
            {
                CementPct = 10,
                WaterPct = 15,
                CoarseAggregatePct = 40,
                FineAggregatePct = 25,
                ScmType = "FlyAsh",
                ScmPct = 10,
            }
        });

        var response = await client.PutAsJsonAsync("/api/batches/VMDE-355", new
        {
            CastAt = DateTime.UtcNow.AddDays(-4),
            FinalWeightLb = 31, // change
            CementType = Domain.Enums.CementType.TypeI, // change
            MixWaterPh = 6.5, // change
            ExpectedFinalBreakPsi = 3600,
            IsSynthetic = false,
            Oxides = new
            {
                IronOxidePct = 3.5,
                AluminaPct = 13.53,
                MagnesiumOxidePct = 5.5,
                SulfurTrioxidePct = 0.2
            },
            Composition = new
            {
                CementPct = 10,
                WaterPct = 15,
                CoarseAggregatePct = 40,
                FineAggregatePct = 25,
                ScmType = "FlyAsh",
                ScmPct = 10,
            }
        });

        response
            .StatusCode
            .ShouldBe(System.Net.HttpStatusCode.OK);
        var content = await response
            .Content
            .ReadFromJsonAsync<UpdateBatchResponse>(_jsonOptions);
        content
            .ShouldNotBeNull()
            .Warnings
            .ShouldContain(w => w.Key == "Batch.MixWaterPh");
    }

    [Fact]
    public async Task Update_unknown_number_is_not_found()
    {
        await using var app = await StartAsync();
        var client = ClientFor(app);

        var response = await client.PutAsJsonAsync("/api/batches/NOT-FOUND", new
        {
            CastAt = DateTime.UtcNow.AddDays(-4),
            FinalWeightLb = 31, // change
            CementType = Domain.Enums.CementType.TypeI, // change
            MixWaterPh = 6.5, // change
            ExpectedFinalBreakPsi = 3600,
            IsSynthetic = false,
            Oxides = new
            {
                IronOxidePct = 3.5,
                AluminaPct = 13.53,
                MagnesiumOxidePct = 5.5,
                SulfurTrioxidePct = 0.2
            },
            Composition = new
            {
                CementPct = 10,
                WaterPct = 15,
                CoarseAggregatePct = 40,
                FineAggregatePct = 25,
                ScmType = "FlyAsh",
                ScmPct = 10,
            }
        });

        response
            .StatusCode
            .ShouldBe(System.Net.HttpStatusCode.NotFound);
        var content = await response
            .Content
            .ReadFromJsonAsync<ValidationProblemDetails>(_jsonOptions);
        content
            .ShouldNotBeNull()
            .Errors
            .ShouldContain(w => w.Key == "Batch.BatchNumber");
    }

    // GET /api/batches
    [Fact]
    public async Task Get_batch_list_no_params_returns_all_batches()
    {
        await using var app = await StartAsync();
        var client = ClientFor(app);

        await client.PostAsJsonAsync("/api/batches", new
        {
            Batch = new
            {
                BatchNumber = "VMDE-355",
                CastAt = DateTime.UtcNow.AddDays(-4),
                FinalWeightLb = 30,
                CementType = Domain.Enums.CementType.TypeII,
                MixWaterPh = 7.0,
                ExpectedFinalBreakPsi = 3600,
                IsSynthetic = false
            },
            Oxides = new
            {
                IronOxidePct = 3.5,
                AluminaPct = 13.53,
                MagnesiumOxidePct = 5.5,
                SulfurTrioxidePct = 0.2
            },
            Composition = new
            {
                CementPct = 10,
                WaterPct = 15,
                CoarseAggregatePct = 40,
                FineAggregatePct = 25,
                ScmType = "FlyAsh",
                ScmPct = 10,
            }
        });
        await client.PostAsJsonAsync("/api/batches", new
        {
            Batch = new
            {
                BatchNumber = "VMDE-356",
                CastAt = DateTime.UtcNow.AddDays(-4),
                FinalWeightLb = 30,
                CementType = Domain.Enums.CementType.TypeII,
                MixWaterPh = 7.0,
                ExpectedFinalBreakPsi = 3600,
                IsSynthetic = false
            },
            Oxides = new
            {
                IronOxidePct = 3.5,
                AluminaPct = 13.53,
                MagnesiumOxidePct = 5.5,
                SulfurTrioxidePct = 0.2
            },
            Composition = new
            {
                CementPct = 10,
                WaterPct = 15,
                CoarseAggregatePct = 40,
                FineAggregatePct = 25,
                ScmType = "FlyAsh",
                ScmPct = 10,
            }
        });
        await client.PostAsJsonAsync("/api/batches", new
        {
            Batch = new
            {
                BatchNumber = "VMDE-357",
                CastAt = DateTime.UtcNow.AddDays(-4),
                FinalWeightLb = 30,
                CementType = Domain.Enums.CementType.TypeII,
                MixWaterPh = 7.0,
                ExpectedFinalBreakPsi = 3600,
                IsSynthetic = false
            },
            Oxides = new
            {
                IronOxidePct = 3.5,
                AluminaPct = 13.53,
                MagnesiumOxidePct = 5.5,
                SulfurTrioxidePct = 0.2
            },
            Composition = new
            {
                CementPct = 10,
                WaterPct = 15,
                CoarseAggregatePct = 40,
                FineAggregatePct = 25,
                ScmType = "FlyAsh",
                ScmPct = 10,
            }
        });

        var response = await client.GetAsync("/api/batches");

        response
            .StatusCode
            .ShouldBe(System.Net.HttpStatusCode.OK);

        var content = await response
            .Content
            .ReadFromJsonAsync<GetBatchListResponse>(_jsonOptions);
        content
            .ShouldNotBeNull()
            .Count
            .ShouldBe(3);
    }

    [Fact]
    public async Task Get_batch_list_includes_synthetic_by_default()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        const string batchNumber = "VM-SYN-DEFAULT-001";

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();

            var httpContextAccessor = scope.ServiceProvider
                .GetRequiredService<IHttpContextAccessor>();

            httpContextAccessor.HttpContext = new DefaultHttpContext();
            httpContextAccessor.HttpContext.Request.Headers[TechnicianHeader.Name] = "Test Technician";

            db.Batch.Add(new Batch
            {
                BatchNumber = batchNumber,
                CastAt = DateTime.UtcNow.AddDays(-1),
                FinalWeightLb = 100m,
                ExpectedFinalBreakPsi = 4500,
                CementType = CementType.TypeI,
                MixWaterPh = 7.2m,
                IsSynthetic = true
            });

            await db.SaveChangesAsync();
        }

        async Task<GetBatchListResponse> SearchAsync(string query)
        {
            var response = await client.GetAsync(query);
            response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

            var batches = await response.Content
                .ReadFromJsonAsync<GetBatchListResponse>(_jsonOptions);

            batches.ShouldNotBeNull();
            return batches;
        }

        var defaultResults = await SearchAsync(
            $"/api/batches?search={batchNumber}");

        defaultResults.Count.ShouldBe(1);
        defaultResults.Single().IsSynthetic.ShouldBeTrue();

        var excludedResults = await SearchAsync(
            $"/api/batches?search={batchNumber}&includeSynthetic=false");

        excludedResults.ShouldBeEmpty();

        var includedResults = await SearchAsync(
            $"/api/batches?search={batchNumber}&includeSynthetic=true");

        includedResults.Count.ShouldBe(1);
    }

    // Verifies that the batch list endpoint rejects page numbers below 1
    // with HTTP 400 and a validation error identifying the page parameter.
    [Fact]
    public async Task Get_batch_list_invalid_page_returns_bad_request()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var response = await client.GetAsync("/api/batches?page=0");

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);

        var content = await response.Content
            .ReadFromJsonAsync<ValidationProblemDetails>(_jsonOptions);

        content.ShouldNotBeNull();
        content.Errors.ShouldContainKey("page");
    }

    [Fact]
    public async Task Get_batch_list_returns_correct_summary_statistics()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        const string batchNumber = "VM-SUMMARY-001";

        // Arrange: create a batch with three cylinders.
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();

            var httpContextAccessor = scope.ServiceProvider
                .GetRequiredService<IHttpContextAccessor>();

            httpContextAccessor.HttpContext = new DefaultHttpContext();
            httpContextAccessor.HttpContext.Request.Headers[TechnicianHeader.Name] = "Test Technician";

            var batch = new Batch
            {
                BatchNumber = batchNumber,
                CastAt = DateTime.UtcNow.AddDays(-28),
                FinalWeightLb = 100m,
                ExpectedFinalBreakPsi = 4500,
                CementType = CementType.TypeI,
                MixWaterPh = 7.2m,
                IsSynthetic = false,
                Specimens =
                [
                    new Specimen
                {
                    Label = "28-A",
                    LengthIn = 12m,
                    WidthIn = 6m,
                    TestAgeDays = TestAgeDays.Day28,
                    BreakResult = new BreakResult
                    {
                        BrokenAt = DateTime.UtcNow,
                        StrengthPsi = 4000
                    }
                },
                new Specimen
                {
                    Label = "28-B",
                    LengthIn = 12m,
                    WidthIn = 6m,
                    TestAgeDays = TestAgeDays.Day28,
                    BreakResult = new BreakResult
                    {
                        BrokenAt = DateTime.UtcNow,
                        StrengthPsi = 5000
                    }
                },
                new Specimen
                {
                    Label = "28-C",
                    LengthIn = 12m,
                    WidthIn = 6m,
                    TestAgeDays = TestAgeDays.Day28
                }
                ]
            };

            db.Batch.Add(batch);
            await db.SaveChangesAsync();

            // Persist two warnings associated with this batch.
            db.RecordWarning.AddRange(
                new RecordWarning
                {
                    EntityType = nameof(Batch),
                    EntityId = batch.Id,
                    FieldName = "MixWaterPh",
                    LimitKey = "Batch.MixWaterPh",
                    Value = 8m,
                    Message = "Test warning 1"
                },
                new RecordWarning
                {
                    EntityType = nameof(Batch),
                    EntityId = batch.Id,
                    FieldName = "MixWaterPh",
                    LimitKey = "Batch.MixWaterPh",
                    Value = 8.5m,
                    Message = "Test warning 2"
                }
            );

            await db.SaveChangesAsync();
        }

        // Act: retrieve the summary through the actual HTTP endpoint.
        var response = await client.GetAsync(
            $"/api/batches?search={batchNumber}");

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        var summaries = await response.Content
            .ReadFromJsonAsync<GetBatchListResponse>(_jsonOptions);

        summaries.ShouldNotBeNull();
        summaries.Count.ShouldBe(1);

        var summary = summaries.Single();

        // Assert: values must reflect persisted database records.
        summary.BatchNumber.ShouldBe(batchNumber);
        summary.CylinderCount.ShouldBe(3);
        summary.BreaksRecorded.ShouldBe(2);
        summary.Average28DayPsi.ShouldBe(4500);
        summary.WarningCount.ShouldBe(2);

        // A batch with persisted warnings must match the warning filter.
        var withWarningsResponse = await client.GetAsync(
            $"/api/batches?search={batchNumber}&hasWarnings=true");

        withWarningsResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        var withWarnings = await withWarningsResponse.Content
            .ReadFromJsonAsync<GetBatchListResponse>(_jsonOptions);

        withWarnings.ShouldNotBeNull();
        withWarnings.Count.ShouldBe(1);

        // The same batch must be excluded when filtering for no warnings.
        var withoutWarningsResponse = await client.GetAsync(
            $"/api/batches?search={batchNumber}&hasWarnings=false");

        withoutWarningsResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        var withoutWarnings = await withoutWarningsResponse.Content
            .ReadFromJsonAsync<GetBatchListResponse>(_jsonOptions);

        withoutWarnings.ShouldNotBeNull();
        withoutWarnings.ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_batch_list_counts_break_result_warnings()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        const string batchNumber = "VM-BREAK-WARN-001";

        // Arrange: create a batch with a 2,500 PSI break result.
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();

            var httpContextAccessor = scope.ServiceProvider
                .GetRequiredService<IHttpContextAccessor>();

            httpContextAccessor.HttpContext = new DefaultHttpContext();
            httpContextAccessor.HttpContext.Request.Headers[TechnicianHeader.Name] =
                "Test Technician";

            var batch = new Batch
            {
                BatchNumber = batchNumber,
                CastAt = DateTime.UtcNow.AddDays(-28),
                FinalWeightLb = 100m,
                ExpectedFinalBreakPsi = 4500,
                CementType = CementType.TypeI,
                MixWaterPh = 7.2m,
                IsSynthetic = false,
                Specimens =
                [
                    new Specimen
                {
                    Label = "28-A",
                    LengthIn = 12m,
                    WidthIn = 6m,
                    TestAgeDays = TestAgeDays.Day28,
                    BreakResult = new BreakResult
                    {
                        BrokenAt = DateTime.UtcNow,
                        StrengthPsi = 2500
                    }
                }
                ]
            };

            db.Batch.Add(batch);
            await db.SaveChangesAsync();

            var breakResult = batch.Specimens.Single().BreakResult;
            breakResult.ShouldNotBeNull();

            // Persist a warning against the break result, not the batch.
            db.RecordWarning.Add(new RecordWarning
            {
                EntityType = nameof(BreakResult),
                EntityId = breakResult.Id,
                FieldName = "StrengthPsi",
                LimitKey = "BreakResult.StrengthPsi",
                Value = 2500m,
                Message = "Break strength is below the expected range."
            });

            await db.SaveChangesAsync();
        }

        // Act: retrieve the batch through the HTTP endpoint.
        var response = await client.GetAsync(
            $"/api/batches?search={batchNumber}");

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        var summaries = await response.Content
            .ReadFromJsonAsync<GetBatchListResponse>(_jsonOptions);

        summaries.ShouldNotBeNull();
        summaries.Count.ShouldBe(1);

        var summary = summaries.Single();

        // The break warning must count toward the parent batch.
        summary.BatchNumber.ShouldBe(batchNumber);
        summary.BreaksRecorded.ShouldBe(1);
        summary.WarningCount.ShouldBe(1);

        // The batch must appear when filtering for warnings.
        var withWarningsResponse = await client.GetAsync(
            $"/api/batches?search={batchNumber}&hasWarnings=true");

        withWarningsResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        var withWarnings = await withWarningsResponse.Content
            .ReadFromJsonAsync<GetBatchListResponse>(_jsonOptions);

        withWarnings.ShouldNotBeNull();
        withWarnings.Count.ShouldBe(1);
        withWarnings.Single().WarningCount.ShouldBe(1);

        // The batch must not appear when filtering for no warnings.
        var withoutWarningsResponse = await client.GetAsync(
            $"/api/batches?search={batchNumber}&hasWarnings=false");

        withoutWarningsResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        var withoutWarnings = await withoutWarningsResponse.Content
            .ReadFromJsonAsync<GetBatchListResponse>(_jsonOptions);

        withoutWarnings.ShouldNotBeNull();
        withoutWarnings.ShouldBeEmpty();
    }

    private async Task<WebApplication> StartAsync()
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

        var client = new HttpClient { BaseAddress = new Uri(address) };
        client.DefaultRequestHeaders.Add(TechnicianHeader.Name, "Deven Vironment");
        return client;
    }
}
