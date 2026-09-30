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

        await app.StartAsync();

        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();

        var address = addresses?.Addresses.Single();

        address.ShouldNotBeNull();

        using var client = new HttpClient
        {
            BaseAddress = new Uri(address)
        };

        var response = await client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.ShouldBeTrue();
        content.ShouldBe("Hello World!");

        await app.StopAsync();
    }
}
