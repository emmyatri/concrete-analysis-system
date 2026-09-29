using System.IO;
using System.Windows;
using Serilog;

namespace Vulcan.Desktop;

public partial class App : Application
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VulcanCBAS",
        "logs");

    private static readonly string LogPath = Path.Combine(
        LogDirectory,
        "app-.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        // Ensure the application log directory exists before configuring Serilog.
        Directory.CreateDirectory(LogDirectory);

        // Write one log file per day and retain the most recent 14 files.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                LogPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();

        Log.Information("Vulcan CBAS application starting");

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Vulcan CBAS application shutting down");

        // Flush any buffered log events before the process exits.
        Log.CloseAndFlush();

        base.OnExit(e);
    }
}

