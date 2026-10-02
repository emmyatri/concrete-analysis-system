using Microsoft.EntityFrameworkCore;
using Vulcan.Infrastructure;

namespace Vulcan.Api;

public static class ApiHost
{
    public static WebApplication Create(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Bind only to this computer and let the OS select an available port.
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDatabase(builder.Configuration);

        var app = builder.Build();
        app.EnsureMigrated();

        // skip seed data in ApiHostTests.
        // will fail due to migration connection not persisting to seeding
        if (!builder.Environment.IsEnvironment("Testing"))
        {
            app.SeedData();
        }

        app.MapGet("/", () => "Hello World!");

        return app;
    }

    private static void EnsureMigrated(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();
        db.Database.Migrate();
    }

    private static void SeedData(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();

        db.SeedValidationLimits();
        db.SeedSpecificGravities();
    }
}
