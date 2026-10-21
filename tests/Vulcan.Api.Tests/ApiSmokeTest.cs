using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Vulcan.Api.Tests;

public class ApiSmokeTests
{
    [Fact]
    public async Task Api_StartsAndResponds()
    {
        // Testing environment = in-memory database, never the real vulcan.db.
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        response.IsSuccessStatusCode.ShouldBeTrue();
    }
}
