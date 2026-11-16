using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vulcan.Api.Technicians;

namespace Vulcan.Api.Settings;

/// <summary>
/// The cloud folder (SCRUM-214, ADR-007): a folder that OneDrive, Google Drive or another sync app keeps
/// online. "Save to cloud" writes reports and exports there and the sync app uploads them, so CBAS itself
/// never needs the internet (NFR-3) and never uploads anything on its own (SC-6).
/// </summary>
public static class CloudFolderEndpoints
{
    public const string Route = "/settings/cloud-folder";

    /// <summary>
    /// GET    /api/settings/cloud-folder → { "path": "C:\\Users\\...\\OneDrive\\Lab", "exists": true } (path null when not set)
    /// PUT    /api/settings/cloud-folder { "path": "..." } → 204, or 400 unless it's a full path to an existing folder
    /// DELETE /api/settings/cloud-folder → 204
    /// </summary>
    public static RouteGroupBuilder MapCloudFolderEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet(Route, (TechnicianSettings settings) =>
        {
            var folder = settings.CloudFolder;
            return Results.Ok(new CloudFolderResponse(folder, folder is not null && Directory.Exists(folder)));
        });

        api.MapPut(Route, (CloudFolderRequest request, TechnicianSettings settings) =>
        {
            var folder = request.Path?.Trim();
            var error = Validate(folder);
            if (error is not null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["CloudFolder.Path"] = [error],
                });
            }

            settings.SaveCloudFolder(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder!)));
            return Results.NoContent();
        });

        api.MapDelete(Route, (TechnicianSettings settings) =>
        {
            settings.SaveCloudFolder(null);
            return Results.NoContent();
        });

        return api;
    }

    private static string? Validate(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return "Choose a folder.";
        }

        if (!Path.IsPathFullyQualified(folder))
        {
            return "Choose a full folder path, such as C:\\Users\\you\\OneDrive\\CBAS.";
        }

        if (!Directory.Exists(folder))
        {
            return "That folder doesn't exist.";
        }

        return null;
    }

    public sealed record CloudFolderRequest(string? Path);

    public sealed record CloudFolderResponse(string? Path, bool Exists);
}
