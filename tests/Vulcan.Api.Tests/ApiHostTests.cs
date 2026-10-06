using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulcan.Infrastructure;
using Vulcan.Infrastructure.Seed;

namespace Vulcan.Api.Tests;

/// <summary>
/// Checks how the in-process API starts, where it listens and how it shuts down.
/// UI serving is covered in <see cref="ApiHostUiTests"/>.
/// </summary>
[Collection("NonParallel")]
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

    // Synthetic data is disabled by default. Starting the API without
    // SeedSyntheticData enabled must leave the Batch table empty.
    [Fact]
    public async Task Synthetic_seed_data_is_disabled_by_default()
    {
        await using var app = ApiHost.Create(_args);

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();

        db.Batch.ShouldBeEmpty();
    }

    // M1-24 requires the approved synthetic dataset to be loaded on startup
    // when SeedSyntheticData is enabled and the database is empty.
    // The demo machine enables this setting through an environment variable.
    [Fact]
    public async Task Synthetic_seed_data_loads_when_environment_variable_is_enabled()
    {
        const string settingName = "SeedSyntheticData";
        var originalValue = Environment.GetEnvironmentVariable(settingName);

        try
        {
            Environment.SetEnvironmentVariable(settingName, "true");

            await using var app = ApiHost.Create(_args);

            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();

            var batches = await db.Batch.ToListAsync();

            batches.Count.ShouldBe(9);
            batches.ShouldAllBe(batch => batch.IsSynthetic);
        }
        finally
        {
            // Restore the original process environment so this test cannot
            // affect other API tests running in the same test process.
            Environment.SetEnvironmentVariable(settingName, originalValue);
        }
    }

    // M1-24 requires synthetic data to load only into an empty database.
    // If any batch already exists, enabling SeedSyntheticData must not
    // insert the demo dataset or modify the existing batch data.
    [Fact]
    public async Task Synthetic_seed_data_does_not_load_when_database_has_batches()
    {
        await using var app = ApiHost.Create(_args);

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();

            db.Batch.Add(new Domain.Entities.Batch
            {
                BatchNumber = "REAL-001",
                CastAt = DateTime.UtcNow,
                FinalWeightLb = 100m,
                ExpectedFinalBreakPsi = 4000,
                CementType = Domain.Enums.CementType.TypeI,
                MixWaterPh = 7.2m,
                IsSynthetic = false
            });

            await db.SaveChangesAsync();

            // Call the seeder directly with the setting conceptually enabled.
            // Because a batch already exists, it must leave the database unchanged.
            SyntheticDataSeeder.Seed(db);

            (await db.Batch.CountAsync()).ShouldBe(1);

            var batch = await db.Batch.SingleAsync();
            batch.BatchNumber.ShouldBe("REAL-001");
            batch.IsSynthetic.ShouldBeFalse();
        }
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
