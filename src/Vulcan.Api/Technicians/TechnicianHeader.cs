using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Vulcan.Domain.Technicians;

namespace Vulcan.Api.Technicians;

/// <summary>
/// Every API call carries the technician's name in this header (M1-42).
/// The UI URL-encodes it, because headers only allow plain ASCII and names may not be ("José").
/// </summary>
public static class TechnicianHeader
{
    public const string Name = "X-Technician";

    /// <summary>
    /// Rejects any save (POST, PUT, PATCH, DELETE under /api) that has no valid technician name, even if the UI
    /// already checked (NFR-6). Saving the name itself is the one exception: that's how a session starts.
    /// </summary>
    public static IApplicationBuilder RequireTechnicianOnSaves(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var request = context.Request;
            var isSave = !HttpMethods.IsGet(request.Method)
                && !HttpMethods.IsHead(request.Method)
                && !HttpMethods.IsOptions(request.Method);
            var isApi = request.Path.StartsWithSegments("/api", out var rest);
            var isStartingSession = HttpMethods.IsPut(request.Method)
                && rest.Equals(TechnicianEndpoints.Route, StringComparison.OrdinalIgnoreCase);

            if (isSave && isApi && !isStartingSession)
            {
                var name = Read(request);
                var error = TechnicianName.Validate(name);
                if (error is not null)
                {
                    await Results.ValidationProblem(
                        new Dictionary<string, string[]> { ["Technician"] = [error] },
                        title: "Every save needs the technician's name.").ExecuteAsync(context);
                    return;
                }
            }

            await next(context);
        });

    /// <summary>
    /// The technician name from the request header, decoded (not trimmed or checked).
    /// Null if the header is missing or badly encoded. ICurrentTechnician (M1-22) can build on this.
    /// </summary>
    public static string? Read(HttpRequest request)
    {
        var raw = request.Headers[Name].ToString();
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        try
        {
            return Uri.UnescapeDataString(raw);
        }
        catch (UriFormatException)
        {
            return null;
        }
    }
}
