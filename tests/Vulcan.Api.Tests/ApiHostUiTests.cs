using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Vulcan.Api.Tests;

/// <summary>
/// Checks that the API serves the Angular app and still answers under /api.
/// Uses a temporary folder with a fake index.html, so the tests don't need a real Angular build.
/// </summary>
public class ApiHostUiTests : IDisposable
{
    private const string _angularMarker = "ANGULAR APP";

    private readonly string[] _args = ["--environment=Testing"];
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"vulcan-wwwroot-{Guid.NewGuid():N}");

    public ApiHostUiTests()
    {
        Directory.CreateDirectory(_webRoot);
        File.WriteAllText(
            Path.Combine(_webRoot, "index.html"),
            $"<!doctype html><html><body>{_angularMarker}</body></html>");
    }

    public void Dispose() => Directory.Delete(_webRoot, recursive: true);

    [Fact]
    public async Task Root_returns_the_angular_page()
    {
        await using var app = ApiHost.Create(_args, _webRoot);
        using var client = await StartAsync(app);

        var response = await client.GetAsync("/");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
        (await response.Content.ReadAsStringAsync()).ShouldContain(_angularMarker);
    }

    [Fact]
    public async Task Unknown_ui_route_returns_the_angular_page()
    {
        await using var app = ApiHost.Create(_args, _webRoot);
        using var client = await StartAsync(app);

        var response = await client.GetAsync("/batches/5");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain(_angularMarker);
    }

    [Fact]
    public async Task Api_route_returns_json()
    {
        await using var app = ApiHost.Create(_args, _webRoot);
        using var client = await StartAsync(app);

        var response = await client.GetAsync("/api/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        (await response.Content.ReadAsStringAsync()).ShouldContain("\"status\":\"ok\"");
    }

    [Fact]
    public async Task Unknown_api_route_returns_404_not_the_angular_page()
    {
        await using var app = ApiHost.Create(_args, _webRoot);
        using var client = await StartAsync(app);

        var response = await client.GetAsync("/api/does-not-exist");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldNotContain(_angularMarker);
    }

    private static async Task<HttpClient> StartAsync(WebApplication app)
    {
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.Single();

        return new HttpClient { BaseAddress = new Uri(address) };
    }
}
