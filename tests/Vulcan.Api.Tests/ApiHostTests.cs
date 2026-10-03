using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulcan.Infrastructure;

namespace Vulcan.Api.Tests;

/// <summary>
/// Checks how the in-process API starts, where it listens and how it shuts down.
/// UI serving is covered in <see cref="ApiHostUiTests"/>.
/// </summary>
public class ApiHostTests
{
    private readonly string[] _args = ["--environment=Testing"];

    [Fact]
    public async Task Database_is_not_real()
    {
        await using var app = ApiHost.Create(_args);

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();

        db.Database.GetConnectionString().ShouldBe("Filename=:memory:");
    }

    [Fact]
    public async Task Seed_data_exists()
    {
        await using var app = ApiHost.Create(_args);

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();

        db.ValidationLimit.ShouldNotBeEmpty();
        db.SpecificGravity.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task StartAsync_AcceptsHttpRequests()
    {
        await using var app = ApiHost.Create(_args);
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = GetListeningAddress(app) };

        var response = await client.GetAsync("/api/health");

        response.IsSuccessStatusCode.ShouldBeTrue();
        (await response.Content.ReadAsStringAsync()).ShouldContain("\"status\":\"ok\"");

        await app.StopAsync();
    }

    [Fact]
    public async Task StartAsync_BindsToLoopbackOnDynamicPort()
    {
        await using var app = ApiHost.Create(_args);
        await app.StartAsync();

        var address = GetListeningAddress(app);

        // Only this computer can reach the API (IPv4 loopback),
        // and the OS picks the port, so nothing is hard-coded.
        address.Scheme.ShouldBe(Uri.UriSchemeHttp);
        address.Host.ShouldBe("127.0.0.1");
        address.Port.ShouldBeGreaterThan(0);

        await app.StopAsync();
    }

    [Fact]
    public async Task StopAsync_StopsAcceptingHttpRequests()
    {
        await using var app = ApiHost.Create(_args);
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = GetListeningAddress(app) };

        // Reachable before shutdown...
        (await client.GetAsync("/api/health")).IsSuccessStatusCode.ShouldBeTrue();

        await app.StopAsync();

        // ...and refuses connections after.
        await Should.ThrowAsync<HttpRequestException>(async () => await client.GetAsync("/api/health"));
    }

    /// <summary>
    /// Returns the address Kestrel actually bound to. The port is chosen by the OS at startup.
    /// </summary>
    private static Uri GetListeningAddress(WebApplication app)
    {
        var addresses = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>();

        var address = addresses?.Addresses.Single();
        address.ShouldNotBeNull();

        return new Uri(address);
    }
}
