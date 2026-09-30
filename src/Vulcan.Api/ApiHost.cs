using Microsoft.AspNetCore.Builder;
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
        builder.Services.AddDatabase();

        var app = builder.Build();
        app.EnsureMigrated();

        app.MapGet("/", () => "Hello World!");

        return app;
    }

    private static void EnsureMigrated(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VulcanDbContext>();
        db.Database.Migrate();
    }
}
