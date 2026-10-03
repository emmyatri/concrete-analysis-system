using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vulcan.Domain.Entities;

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

        services.AddDbContext<VulcanDbContext>(o => o.UseSqlite(cs));
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
}
