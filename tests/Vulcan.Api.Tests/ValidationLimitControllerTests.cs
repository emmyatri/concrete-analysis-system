using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Vulcan.Api;
using Vulcan.Application.Contracts;
using Vulcan.Domain.Constants;

public class ValidationLimitControllerTests : IDisposable
{
    public void Dispose()
    {
    }

    [Fact]
    public async Task Get_validation_limits_returns_seed_data()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var response = await client.GetAsync("/api/validation-limits");

        response
            .ShouldNotBeNull()
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var deserialized = await response
            .Content
            .ReadFromJsonAsync<GetValidationListResponse>();
        deserialized
            .ShouldNotBeNull()
            .ShouldContain(x => x.Key == LimitKeys.CementPct && x.Min == 10.0 && x.Max == 15.0);
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
