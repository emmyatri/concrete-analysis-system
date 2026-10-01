using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Vulcan.Infrastructure;

public static class SetupUtils
{
    /// <summary>
    /// Initializes the Sqlite database at %APPDATA%/VulcanCBAS/vulcan.db and registers it with the builder services.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration config)
    {
        // create the database at %APPDATA%/VulcanCBAS/vulcan.db
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
}
