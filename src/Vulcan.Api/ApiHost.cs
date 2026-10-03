using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Vulcan.Infrastructure;

namespace Vulcan.Api;

public static class ApiHost
{
    /// <summary>
    /// Creates the in-process API. It also serves the built Angular app, so the UI and the API share one local address.
    /// </summary>
    /// <param name="args">Command-line arguments, e.g. --environment=Testing.</param>
    /// <param name="webRootPath">
    /// Folder that holds the built Angular app. Defaults to the wwwroot folder next to the running program.
    /// Tests pass their own folder.
    /// </param>
    /// <param name="port">
    /// 0 (default) lets the OS choose. Only live-reload mode uses a fixed port.
    /// </param>
    /// <param name="configureServices">
    /// Optional override for builder service configuration.
    /// </param>
    public static WebApplication Create(
        string[] args,
        string? webRootPath = null,
        int port = 0,
        Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Bind only to this computer and let the OS select an available port.
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.Services.AddDatabase(builder.Configuration);

        // use overrides if provided
        configureServices?.Invoke(builder.Services);

        var app = builder.Build();
        app.EnsureMigrated();
        app.SeedData();

        // Angular files: index.html, scripts, styles, fonts and images.
        webRootPath ??= Path.Combine(AppContext.BaseDirectory, "wwwroot");
        Directory.CreateDirectory(webRootPath);
        var uiFiles = new PhysicalFileProvider(webRootPath);

        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = uiFiles });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = uiFiles });

        // Every API endpoint lives under /api.
        var api = app.MapGroup("/api");
        api.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        // Unknown /api routes return a JSON 404, never the Angular page.
        api.Map("/{**path}", () => Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "API route not found"));

        // Any other path is an Angular route, so return index.html and let Angular handle it.
        app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = uiFiles });

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
