using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Vulcan.Api.Tests;

public class ApiHostTests
{
    [Fact]
    public async Task StartAsync_AcceptsHttpRequests()
    {
        await using var app = ApiHost.Create([]);

        // Start the in-process API and wait until Kestrel is ready
        // to accept requests.
        await app.StartAsync();

        // Retrieve the actual address selected by Kestrel because the
        // application requests a dynamically assigned port.
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();

        var address = addresses?.Addresses.Single();

        address.ShouldNotBeNull();

        using var client = new HttpClient
        {
            BaseAddress = new Uri(address)
        };

        // Verify the in-process server can receive an HTTP request
        // through its dynamically assigned loopback address.
        var response = await client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.ShouldBeTrue();
        content.ShouldBe("Hello World!");

        await app.StopAsync();
    }

    [Fact]
    public async Task StartAsync_BindsToLoopbackOnDynamicPort()
    {
        await using var app = ApiHost.Create([]);

        // Start the in-process server and wait until Kestrel has selected
        // an available port and is ready to accept requests.
        await app.StartAsync();

        // Retrieve the address assigned by Kestrel so the test can verify
        // that the server is restricted to the local machine.
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();

        var address = addresses?.Addresses.Single();

        address.ShouldNotBeNull();

        var uri = new Uri(address);

        // The API must listen only on IPv4 loopback and use an
        // OS-selected port rather than a hard-coded application port.
        uri.Host.ShouldBe("127.0.0.1");
        uri.Port.ShouldBeGreaterThan(0);

        await app.StopAsync();
    }

    [Fact]
    public async Task StopAsync_StopsAcceptingHttpRequests()
    {
        await using var app = ApiHost.Create([]);

        await app.StartAsync();

        // Retrieve the active server address so the same endpoint can
        // be tested both before and after shutdown.
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();

        var address = addresses?.Addresses.Single();

        address.ShouldNotBeNull();

        using var client = new HttpClient
        {
            BaseAddress = new Uri(address)
        };

        // Confirm the server is reachable before attempting shutdown.
        var responseBeforeStop = await client.GetAsync("/");
        responseBeforeStop.IsSuccessStatusCode.ShouldBeTrue();

        // A clean shutdown should stop Kestrel from accepting any
        // additional HTTP requests on the previously assigned address.
        await app.StopAsync();

        await Should.ThrowAsync<HttpRequestException>(
            async () => await client.GetAsync("/"));
    }
}
