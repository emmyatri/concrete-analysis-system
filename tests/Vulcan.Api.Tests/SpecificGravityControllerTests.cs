using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Vulcan.Application.Contracts;

namespace Vulcan.Api.Tests;

public class SpecificGravityControllerTests : IDisposable
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public void Dispose()
    {
    }

    [Fact]
    public async Task Get_specific_gravities_returns_seed_data()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var response = await client.GetAsync("/api/specific-gravities");

        response
            .ShouldNotBeNull()
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var deserialized = await response
            .Content
            .ReadFromJsonAsync<GetSpecificGravityListResponse>(_jsonOptions);
        deserialized.ShouldNotBeNull()
            .ShouldContain(x => x.Material == Domain.Enums.Material.FlyAsh && x.Value == 2.35);
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

        return new HttpClient { BaseAddress = new Uri(address) };
    }
}
