using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Vulcan.Application.Interfaces;
using Vulcan.Application.Services;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Interfaces;
using Vulcan.Domain.Services;
using Vulcan.Infrastructure.Interceptors;
using Vulcan.Infrastructure.Services;

namespace Vulcan.Infrastructure;

public static class SetupUtils
{
    /// <summary>
    /// Initializes the Sqlite database at %LOCALAPPDATA%/VulcanCBAS/vulcan.db and registers it with the builder services.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<AuditSaveChangesInterceptor>();

        // create the database at %LOCALAPPDATA%/VulcanCBAS/vulcan.db
        // On linux this is ~/.config/VulcanCBAS/vulcan.db
        // Or $HOME/.var/app/com.jetbrains.Rider/config/VulcanCBAS/vulcan.db if using Rider.
        var cs = config.GetConnectionString("Vulcan");
        if (string.IsNullOrWhiteSpace(cs))
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VulcanCBAS");
            Directory.CreateDirectory(dir);
            cs = $"Data Source={Path.Combine(dir, "vulcan.db")}";
        }


        if (cs.Contains(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            // An in-memory database only lives while a connection is open, so keep one open for the app's lifetime (tests).
            services.AddSingleton(_ =>
            {
                var connection = new SqliteConnection(cs);
                connection.Open();
                return connection;
            });
            services.AddDbContext<VulcanDbContext>((provider, options) =>
                options.UseSqlite(provider.GetRequiredService<SqliteConnection>())
                    .AddInterceptors(
                        provider.GetRequiredService<AuditSaveChangesInterceptor>()));
        }
        else
        {
            services.AddDbContext<VulcanDbContext>((provider, options) =>
                options.UseSqlite(cs)
                    .AddInterceptors(
                        provider.GetRequiredService<AuditSaveChangesInterceptor>()));
        }

        services.AddScoped<ILimitProvider, LimitProvider>();
        services.AddScoped<ISpecificGravityProvider, SpecificGravityProvider>();
        services.AddScoped<ICalibrationProvider, CalibrationProvider>();
        services.AddScoped<LimitEvaluator>();
        services.AddScoped<WaterCementRatioCalculator>();

        services.AddScoped<IClock, SystemClock>();

        services.AddScoped<IBatchRepository, BatchRepository>();
        services.AddScoped<ISpecimenRepository, SpecimenRepository>();
        services.AddScoped<ICuringRepository, CuringRepository>();
        services.AddScoped<IRecordWarningRepository, RecordWarningRepository>();
        services.AddScoped<ISpecificGravityRepository, SpecificGravityRepository>();
        services.AddScoped<IValidationLimitRepository, ValidationLimitRepository>();

        services.AddScoped<BatchValidator>();
        services.AddScoped<CompositionValidator>();
        services.AddScoped<CementOxideValidator>();
        services.AddScoped<SpecimenValidator>();
        services.AddScoped<BreakResultValidator>();
        services.AddScoped<CuringReadingValidator>();

        services.AddScoped<BatchService>();
        services.AddScoped<SpecimenService>();
        services.AddScoped<CuringService>();
        services.AddScoped<SpecificGravityService>();
        services.AddScoped<ValidationLimitService>();

        return services;
    }

    private static readonly JsonSerializerOptions _jsonOptions = CreateJsonSerializerOptions();

    private static JsonSerializerOptions CreateJsonSerializerOptions()
    {
        var jsonOptions = new JsonSerializerOptions();
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        return jsonOptions;
    }

    public static List<ValidationLimit> LoadValidationLimits()
    {
        var json = (
            typeof(SetupUtils).Assembly.GetManifestResourceStream("Vulcan.Infrastructure.Seed.validation-limits.json")
        ) ?? throw new InvalidOperationException(
            "Could not find validation-limits file. Please ensure it is in Vulcan.Infrastructure/Seed/validation-limits.json");
        var validationLimits = JsonSerializer.Deserialize<List<ValidationLimit>>(json, _jsonOptions)
                               ?? throw new JsonException("Validation Limit seed data is malformed");
        return validationLimits;
    }

    public static void SeedValidationLimits(this VulcanDbContext db)
    {
        var limits = LoadValidationLimits();

        var existing = db.ValidationLimit
            .Select(x => new { x.Key, x.Severity })
            .ToList();

        var missing = limits.Where(seed =>
            !existing.Any(x =>
                x.Key == seed.Key &&
                x.Severity == seed.Severity));

        db.ValidationLimit.AddRange(missing);
        db.SaveChanges();
    }

    public static List<SpecificGravity> LoadSpecificGravities()
    {
        var json = (
            typeof(SetupUtils).Assembly.GetManifestResourceStream("Vulcan.Infrastructure.Seed.specific-gravity.json")
        ) ?? throw new InvalidOperationException(
            "Could not the specific-gravity file. Please ensure it is in Vulcan.Infrastructure/Seed/specific-gravity.json");
        var specificGravities = JsonSerializer.Deserialize<List<SpecificGravity>>(json, _jsonOptions)
                                ?? throw new JsonException("Specific Gravity seed data is malformed");
        return specificGravities;
    }

    public static void SeedSpecificGravities(this VulcanDbContext db)
    {
        var gravities = LoadSpecificGravities();

        var existing = db.SpecificGravity
            .Select(x => x.Material)
            .ToList();

        var missing = gravities.Where(seed =>
            !existing.Contains(seed.Material));
        db.SpecificGravity.AddRange(missing);
        db.SaveChanges();
    }

    public static CalibrationVersion LoadCalibrationVersionV1()
    {
        var json = (
            typeof(SetupUtils).Assembly.GetManifestResourceStream("Vulcan.Infrastructure.Seed.calibration-v1.json")
        ) ?? throw new InvalidOperationException(
            "Could not find the calibration v1 file. Please ensure it is in Vulcan.Infrastructure/Seed/calibration-v1.json");
        var calibration = JsonSerializer.Deserialize<CalibrationVersion>(json, _jsonOptions)
                                ?? throw new JsonException("Calibration v1 seed data is malformed");
        return calibration;
    }

    public static void SeedCalibrationVersionV1(this VulcanDbContext db)
    {
        var calibration = LoadCalibrationVersionV1();

        var existing = db.CalibrationVersion
            .ToList()
            .FirstOrDefault(x => string.Equals(x.Version, calibration.Version, StringComparison.InvariantCultureIgnoreCase));

        if (existing != null)
        {
            return;
        }

        db.Add(calibration);
        db.SaveChanges();
    }
}
