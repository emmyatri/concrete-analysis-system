using System.Windows;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
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

    public IServiceProvider Services =>
        _serviceProvider
        ?? throw new InvalidOperationException(
            "Desktop services are not available until application startup completes.");

    protected override async void OnStartup(StartupEventArgs e)
    {
        _apiHost = ApiHost.Create([]);

        await _apiHost.StartAsync();

        var server = _apiHost.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();

        var serverAddress = addresses?.Addresses.Single()
                            ?? throw new InvalidOperationException(
                                "The API server did not provide a listening address.");

        var services = new ServiceCollection();

        services.AddSingleton(new ApiServerInfo
        {
            BaseAddress = new Uri(serverAddress)
        });

        _serviceProvider = services.BuildServiceProvider();

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

        base.OnExit(e);
    }
}

