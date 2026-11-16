using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Vulcan.Api.Technicians;

namespace Vulcan.Api.Tests;

/// <summary>
/// The cloud folder (SCRUM-214): saved on this computer next to the technician name.
/// Each test gets its own settings folder and its own "cloud" folder, so nothing touches the real ones.
/// </summary>
public sealed class CloudFolderTests : IDisposable
{
    private const string _route = "/api/settings/cloud-folder";

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "vulcan-tests", Guid.NewGuid().ToString("N"));

    private string SettingsDirectory => Path.Combine(_root, "settings");

    private string CloudDirectory => Path.Combine(_root, "OneDrive", "Lab");

    public CloudFolderTests() => Directory.CreateDirectory(CloudDirectory);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Cloud_folder_is_not_set_the_first_time()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var response = await client.GetFromJsonAsync<FolderBody>(_route);

        response!.Path.ShouldBeNull();
        response.Exists.ShouldBeFalse();
    }

    [Fact]
    public async Task Cloud_folder_is_saved_and_survives_a_restart()
    {
        await using (var first = await StartAsync())
        {
            using var client = TechnicianClientFor(first);
            var save = await client.PutAsJsonAsync(_route, new PathBody(CloudDirectory + Path.DirectorySeparatorChar));
            save.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        // A new host reads the same settings file, like reopening the app.
        await using var second = await StartAsync();
        using var again = ClientFor(second);

        var response = await again.GetFromJsonAsync<FolderBody>(_route);

        // Saved without the trailing separator.
        response!.Path.ShouldBe(CloudDirectory);
        response.Exists.ShouldBeTrue();
    }

    [Fact]
    public async Task Saving_the_cloud_folder_keeps_the_last_technician()
    {
        await using var app = await StartAsync();
        using var client = TechnicianClientFor(app);

        await client.PutAsJsonAsync("/api/settings/technician", new { name = "Jordan" });
        await client.PutAsJsonAsync(_route, new PathBody(CloudDirectory));

        var technician = await client.GetFromJsonAsync<NameBody>("/api/settings/technician");
        technician!.Name.ShouldBe("Jordan");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative-folder")]
    public async Task Saving_a_blank_or_relative_path_is_rejected(string folder)
    {
        await using var app = await StartAsync();
        using var client = TechnicianClientFor(app);

        var response = await client.PutAsJsonAsync(_route, new PathBody(folder));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await PathErrorAsync(response)).ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Saving_a_folder_that_does_not_exist_is_rejected()
    {
        await using var app = await StartAsync();
        using var client = TechnicianClientFor(app);

        var response = await client.PutAsJsonAsync(_route, new PathBody(Path.Combine(_root, "missing")));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await PathErrorAsync(response)).ShouldBe("That folder doesn't exist.");
    }

    [Fact]
    public async Task A_cloud_folder_that_was_removed_reads_as_missing()
    {
        await using var app = await StartAsync();
        using var client = TechnicianClientFor(app);
        await client.PutAsJsonAsync(_route, new PathBody(CloudDirectory));

        // e.g. the sync app was uninstalled or the drive was unplugged.
        Directory.Delete(CloudDirectory);

        var response = await client.GetFromJsonAsync<FolderBody>(_route);

        response!.Path.ShouldBe(CloudDirectory);
        response.Exists.ShouldBeFalse();
    }

    [Fact]
    public async Task Clearing_the_cloud_folder_sets_it_back_to_not_set()
    {
        await using var app = await StartAsync();
        using var client = TechnicianClientFor(app);
        await client.PutAsJsonAsync(_route, new PathBody(CloudDirectory));

        var clear = await client.DeleteAsync(_route);
        clear.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await client.GetFromJsonAsync<FolderBody>(_route);
        response!.Path.ShouldBeNull();
    }

    [Fact]
    public async Task Changing_the_cloud_folder_needs_a_technician()
    {
        await using var app = await StartAsync();
        using var client = ClientFor(app);

        var response = await client.PutAsJsonAsync(_route, new PathBody(CloudDirectory));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("\"Technician\":");
    }

    private async Task<WebApplication> StartAsync()
    {
        var app = ApiHost.Create(
            ["--environment=Testing", $"--{TechnicianSettings.DirectoryKey}={SettingsDirectory}"]);
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

    private static HttpClient TechnicianClientFor(WebApplication app)
    {
        var client = ClientFor(app);
        client.DefaultRequestHeaders.Add(TechnicianHeader.Name, "Jordan");
        return client;
    }

    /// <summary>The message for CloudFolder.Path in a 400 ValidationProblem.</summary>
    private static async Task<string?> PathErrorAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("errors").GetProperty("CloudFolder.Path")[0].GetString();
    }

    private sealed record PathBody(string? Path);

    private sealed record FolderBody(string? Path, bool Exists);

    private sealed record NameBody(string? Name);
}
