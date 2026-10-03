using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Vulcan.Api.Technicians;

namespace Vulcan.Api.Tests;

/// <summary>
/// The technician name (M1-42): remembered on this computer, and required on every save.
/// Each test gets its own settings folder, so nothing touches the real %LOCALAPPDATA%.
/// </summary>
public sealed class TechnicianTests : IDisposable
{
    private readonly string _settingsDirectory =
        Path.Combine(Path.GetTempPath(), "vulcan-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_settingsDirectory))
        {
            Directory.Delete(_settingsDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Last_technician_is_empty_the_first_time()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var response = await client.GetFromJsonAsync<NameBody>("/api/settings/technician");

        response!.Name.ShouldBeNull();
    }

    [Fact]
    public async Task Last_technician_is_saved_trimmed_and_survives_a_restart()
    {
        await using (var first = await StartAsync())
        {
            using var client = ClientFor(first);
            var save = await client.PutAsJsonAsync("/api/settings/technician", new NameBody(" Jordan "));
            save.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        // A new host reads the same settings file, like reopening the app.
        await using var second = await StartAsync();
        using var again = ClientFor(second);

        var response = await again.GetFromJsonAsync<NameBody>("/api/settings/technician");

        response!.Name.ShouldBe("Jordan");
    }

    [Theory]
    [InlineData("J")]
    [InlineData("ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJK")] // 51
    public async Task Saving_a_name_outside_2_to_50_characters_is_rejected(string name)
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var response = await client.PutAsJsonAsync("/api/settings/technician", new NameBody(name));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Technician.Name");
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task A_save_with_no_technician_is_rejected(string method)
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/batches")
        {
            Content = JsonContent.Create(new { }),
        };

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("\"Technician\":");
    }

    [Fact]
    public async Task A_save_with_a_technician_gets_through()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);
        client.DefaultRequestHeaders.Add(TechnicianHeader.Name, Uri.EscapeDataString("José Rivera"));

        var response = await client.PostAsJsonAsync("/api/batches", new { });

        // No batches endpoint yet, so it reaches the JSON 404 instead of being stopped at the door.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reading_never_needs_a_technician()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var response = await client.GetAsync("/api/health");

        response.IsSuccessStatusCode.ShouldBeTrue();
    }

    private async Task<WebApplication> StartAsync()
    {
        var app = ApiHost.Create(
            ["--environment=Testing", $"--{TechnicianSettings.DirectoryKey}={_settingsDirectory}"]);
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

    private sealed record NameBody(string? Name);
}
