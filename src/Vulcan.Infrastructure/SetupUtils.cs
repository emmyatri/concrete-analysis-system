using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Vulcan.Infrastructure;

public static class SetupUtils
{
    /// <summary>
    /// Initializes the Sqlite database at %APPDATA%/VulcanDBAS/vulcan.db and registers it with the builder services.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddDatabase(this IServiceCollection services)
    {
        // create the database at %APPDATA%/VulcanCBAS/vulcan.db
        // On linux this is ~/.config/VulcanCBAS/vulcan.db
        // Or $HOME/.var/app/com.jetbrains.Rider/config/VulcanCBAS/vulcan.db if using Rider.
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var directory = Path.Combine(appData, "VulcanCBAS");
        Directory.CreateDirectory(directory);

        var dbPath = Path.Combine(directory, "vulcan.db");

        services.AddDbContext<VulcanDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

        return services;
    }
}
