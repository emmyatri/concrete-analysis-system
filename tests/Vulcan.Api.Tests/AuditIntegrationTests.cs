using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulcan.Api.Technicians;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Interfaces;
using Vulcan.Infrastructure;

namespace Vulcan.Api.Tests;

public sealed class AuditIntegrationTests
{
    [Fact]
    public async Task Startup_uses_system_but_normal_saves_require_a_technician()
    {
        var beforeStartup = DateTime.UtcNow;
        await using var app = ApiHost.Create(["--environment=Testing"]);
        var afterStartup = DateTime.UtcNow;

        using var scope = app.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();
        var technician = scope.ServiceProvider
            .GetRequiredService<ICurrentTechnician>();

        db.Database.GetConnectionString().ShouldBe("Filename=:memory:");

        var limits = await db.ValidationLimit.AsNoTracking().ToListAsync();
        limits.ShouldNotBeEmpty();

        foreach (var limit in limits)
        {
            limit.CreatedBy.ShouldBe("System");
            limit.CreatedAtUtc.ShouldBeGreaterThanOrEqualTo(beforeStartup);
            limit.CreatedAtUtc.ShouldBeLessThanOrEqualTo(afterStartup);
            limit.ModifiedBy.ShouldBeNull();
            limit.ModifiedAtUtc.ShouldBeNull();
        }

        technician.Name.ShouldBeNull();

        var originalBatchCount = await db.Batch.CountAsync();

        db.Batch.Add(new Batch
        {
            BatchNumber = "SYN-AUDIT-INTEGRATION",
            IsSynthetic = true,
        });

        var error = await Should.ThrowAsync<InvalidOperationException>(
            async () => await db.SaveChangesAsync());

        error.Message.ShouldContain("valid technician name");

        db.ChangeTracker.Clear();
        (await db.Batch.CountAsync()).ShouldBe(originalBatchCount);
    }

    [Fact]
    public async Task Request_technician_is_used_for_each_saved_record()
    {
        await using var app = ApiHost.Create(["--environment=Testing"]);

        app.MapPost("/api/audit-test", async (VulcanDbContext db) =>
        {
            var batch = new Batch
            {
                BatchNumber = $"SYN-AUDIT-{Guid.NewGuid():N}",
                IsSynthetic = true,
            };

            db.Batch.Add(batch);
            await db.SaveChangesAsync();

            return Results.Text(batch.CreatedBy);
        });

        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.Single();

        using var client = new HttpClient
        {
            BaseAddress = new Uri(address),
        };

        var beforeRequests = DateTime.UtcNow;

        foreach (var name in new[] { "Daniel", "NotDaniel" })
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post, "/api/audit-test");

            request.Headers.Add(
                TechnicianHeader.Name,
                Uri.EscapeDataString($" {name} "));

            using var response = await client.SendAsync(request);

            response.EnsureSuccessStatusCode();
            (await response.Content.ReadAsStringAsync()).ShouldBe(name);
        }

        var afterRequests = DateTime.UtcNow;

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();

        var saved = await db.Batch.AsNoTracking().ToListAsync();

        saved.Count.ShouldBe(2);
        saved.Select(batch => batch.CreatedBy).ShouldBe(
            new[] { "Daniel", "NotDaniel" }, ignoreOrder: true);

        foreach (var batch in saved)
        {
            batch.CreatedAtUtc.ShouldBeGreaterThanOrEqualTo(beforeRequests);
            batch.CreatedAtUtc.ShouldBeLessThanOrEqualTo(afterRequests);
            batch.ModifiedBy.ShouldBeNull();
            batch.ModifiedAtUtc.ShouldBeNull();
        }

        scope.ServiceProvider.GetRequiredService<ICurrentTechnician>()
            .Name.ShouldBeNull();

        await app.StopAsync();
    }
}
