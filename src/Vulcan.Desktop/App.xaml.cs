using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Serilog;

namespace Vulcan.Desktop;

public partial class App : Application
{
    private static readonly string _logDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VulcanCBAS",
        "logs");

    private static readonly string _logPath = Path.Combine(
        _logDirectory,
        "app-.log");

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled application error");

        MessageBox.Show(
            "An unexpected error occurred. The error has been recorded in the application log.",
            "Vulcan CBAS",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Ensure the application log directory exists before configuring Serilog.
        Directory.CreateDirectory(_logDirectory);

        // Register the global handler for unhandled UI-thread exceptions.
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Write one log file per day and retain the most recent 14 files.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                _logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();

        var version = Assembly.GetExecutingAssembly()
            .GetName()
            .Version?
            .ToString() ?? "Unknown";

        Log.Information("Vulcan CBAS application starting");
        Log.Information("Application version: {Version}", version);
        Log.Information("Log location: {LogDirectory}", _logDirectory);

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

