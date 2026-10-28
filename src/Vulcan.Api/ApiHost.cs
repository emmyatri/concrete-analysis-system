using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi;
using Vulcan.Api.Controllers;
using Vulcan.Api.Technicians;
using Vulcan.Application.Interfaces;
using Vulcan.Domain.Interfaces;
using Vulcan.Infrastructure;
using Vulcan.Infrastructure.Interceptors;
using Vulcan.Infrastructure.Seed;
using Vulcan.Infrastructure.Services;

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


        builder.Services
            .AddLogging()
            .AddDatabase(builder.Configuration)
            .AddSingleton<TechnicianSettings>()
            .AddHttpContextAccessor()
            .AddScoped<ICurrentTechnician, CurrentTechnician>()
            .ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .AddEndpointsApiExplorer()
            .AddSwaggerGen(o =>
            {
                o.SwaggerDoc("v1", new OpenApiInfo()
                {
                    Title = "Vulcan API",
                    Version = "v1",
                });
            })
            .AddControllers()
            // IMPORTANT: required so the desktop can discover the controllers
            .AddApplicationPart(typeof(BatchController).Assembly)
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

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

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(o =>
            {
                o.SwaggerEndpoint("/swagger/v1/swagger.json", "Vulcan API");
                // makes docs visible at http://host:port/api/v1/docs
                o.RoutePrefix = "api/v1/docs";
            });
        }
        // Saves without a technician name are rejected before they reach an endpoint.
        app.RequireTechnicianOnSaves();

        // Every API endpoint lives under /api.
        var api = app.MapGroup("/api");
        api.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        api.MapTechnicianEndpoints();
        app.MapControllers();

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
        var strengthEstimatorService = scope.ServiceProvider.GetRequiredService<IStrengthEstimateService>();
        var calibrationProvider = scope.ServiceProvider.GetRequiredService<ICalibrationProvider>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PredictionSaveChangesInterceptor>>();

        var options = new DbContextOptionsBuilder<VulcanDbContext>()
                    .UseSqlite(db.Database.GetDbConnection())
                    .AddInterceptors(
                        new PredictionSaveChangesInterceptor(
                            _ => calibrationProvider,
                            strengthEstimatorService,
                            logger),
                        // add audit changes last
                        new AuditSaveChangesInterceptor(
                            new SystemTechnician(),
                            scope.ServiceProvider.GetRequiredService<IClock>()))
                    .Options;

        using var seedDb = new VulcanDbContext(options);

        seedDb.SeedValidationLimits();
        seedDb.SeedSpecificGravities();
        seedDb.SeedCalibrationVersionV1();

        if (app.Configuration.GetValue<bool>("SeedSyntheticData"))
        {
            SyntheticDataSeeder.Seed(seedDb);
        }
    }
}
