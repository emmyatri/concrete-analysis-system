using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Vulcan.Api.Technicians;
using Vulcan.Application.Contracts;
using Vulcan.Domain.Entities;

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
            .ShouldBe(System.Net.HttpStatusCode.Conflict);

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
