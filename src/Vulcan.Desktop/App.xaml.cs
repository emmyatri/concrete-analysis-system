using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Vulcan.Api;
using Vulcan.Desktop.Services;

namespace Vulcan.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private WebApplication? _apiHost;
    private ServiceProvider? _serviceProvider;

    private static readonly string _logDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VulcanCBAS",
        "logs");

    private static readonly string _logPath = Path.Combine(
        _logDirectory,
        "app-.log");

    public IServiceProvider Services =>
        _serviceProvider
        ?? throw new InvalidOperationException(
            "Desktop services are not available until application startup completes.");

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

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Ensure the application log directory exists before configuring Serilog.
        Directory.CreateDirectory(_logDirectory);

        // Write one log file per day and retain the most recent 14 files.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                _logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();

        // Register global exception handlers after Serilog is configured.
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log.Fatal(
                args.ExceptionObject as Exception,
                "Unhandled non-UI exception");

            Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(
                args.Exception,
                "Unobserved task exception");

            args.SetObserved();
        };

        var version = Assembly.GetExecutingAssembly()
            .GetName()
            .Version?
            .ToString() ?? "Unknown";

        Log.Information("Vulcan CBAS application starting");
        Log.Information("Application version: {Version}", version);
        Log.Information("Log location: {LogDirectory}", _logDirectory);

        try
        {
            _apiHost = ApiHost.Create([]);

            await _apiHost.StartAsync();

            var server = _apiHost.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>();

            var serverAddress = addresses?.Addresses.Single()
                                ?? throw new InvalidOperationException(
                                    "The API server did not provide a listening address.");

            Log.Information(
                "In-process API host listening at {ServerAddress}",
                serverAddress);

            var services = new ServiceCollection();

            services.AddSingleton(new ApiServerInfo
            {
                BaseAddress = new Uri(serverAddress)
            });

            _serviceProvider = services.BuildServiceProvider();

            Log.Information("In-process API host started successfully");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to start in-process API host");

            if (_apiHost is not null)
            {
                await _apiHost.DisposeAsync();
                _apiHost = null;
            }

            MessageBox.Show(
                "The application server could not be started. " +
                "The error has been recorded in the application log.",
                "Vulcan CBAS",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_apiHost is not null)
        {
            await _apiHost.StopAsync();
            await _apiHost.DisposeAsync();
        }

        _serviceProvider?.Dispose();

        Log.Information("Vulcan CBAS application shutting down");

        // Flush any buffered log events before the process exits.
        Log.CloseAndFlush();

        base.OnExit(e);
    }
}
