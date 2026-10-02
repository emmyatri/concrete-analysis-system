using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
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

    // Fixed API port, used only with the Angular dev server (live reload).
    // Must match src/Vulcan.Web/proxy.conf.json.
    private const int _devApiPort = 5180;

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

        if (!TryGetWebView2Version(out var webView2Version))
        {
            MessageBox.Show(
                "Vulcan CBAS can't start because the Microsoft Edge WebView2 Runtime isn't installed on this computer.\n\n" +
                "Reinstall Vulcan CBAS (the installer includes it). Details are in the application log.",
                "Vulcan CBAS",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown();
            return;
        }

        Log.Information("WebView2 runtime version: {WebView2Version}", webView2Version);

        try
        {
            _apiHost = ApiHost.Create([], port: GetUiDevServer() is null ? 0 : _devApiPort);

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

            var window = new MainWindow(GetUiDevServer() ?? new Uri(serverAddress));
            MainWindow = window;
            window.Show();
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

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Vulcan CBAS application shutting down");

        if (_apiHost is not null)
        {
            try
            {
                // Run on the thread pool so blocking the UI thread can't deadlock.
                Task.Run(async () =>
                {
                    await _apiHost.StopAsync();
                    await _apiHost.DisposeAsync();
                }).Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error stopping in-process API host");
            }
        }

        _serviceProvider?.Dispose();

        // Flush any buffered log events before the process exits.
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    /// <summary>
    /// Returns the WebView2 runtime version, or false if the runtime isn't installed.
    /// </summary>
    private static bool TryGetWebView2Version(out string version)
    {
        try
        {
            version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            return true;
        }
        catch (WebView2RuntimeNotFoundException ex)
        {
            Log.Fatal(ex, "WebView2 runtime is not installed");
            version = string.Empty;
            return false;
        }
    }

    /// <summary>
    /// Debug builds only: if VULCAN_UI_DEV_SERVER is set (e.g. http://localhost:4200),
    /// show the Angular dev server instead of the built files, for live reload.
    /// </summary>
    private static Uri? GetUiDevServer()
    {
#if DEBUG
        var value = Environment.GetEnvironmentVariable("VULCAN_UI_DEV_SERVER");
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
#else
        return null;
#endif
    }
}
