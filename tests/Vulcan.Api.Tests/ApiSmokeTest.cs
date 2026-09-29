using Microsoft.AspNetCore.Mvc.Testing;

namespace Vulcan.Api.Tests;

public class ApiSmokeTests
{
    [Fact]
    public async Task Api_StartsAndResponds()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        response.IsSuccessStatusCode.ShouldBeTrue();
    }
}
