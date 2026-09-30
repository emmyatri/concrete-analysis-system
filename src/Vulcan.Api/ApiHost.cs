using Microsoft.AspNetCore.Builder;

namespace Vulcan.Api;

public static class ApiHost
{
    public static WebApplication Create(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Bind only to this computer and let the OS select an available port.
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var app = builder.Build();

        app.MapGet("/", () => "Hello World!");

        return app;
    }
}
