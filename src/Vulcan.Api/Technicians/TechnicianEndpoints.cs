using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vulcan.Domain.Technicians;

namespace Vulcan.Api.Technicians;

public static class TechnicianEndpoints
{
    public const string Route = "/settings/technician";

    /// <summary>
    /// GET  /api/settings/technician → { "name": "J. Rivera" } (null the first time)
    /// PUT  /api/settings/technician { "name": " J. Rivera " } → 204, saved trimmed
    /// </summary>
    public static RouteGroupBuilder MapTechnicianEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet(Route, (TechnicianSettings settings) =>
            Results.Ok(new TechnicianResponse(settings.LastTechnician)));

        api.MapPut(Route, (TechnicianRequest request, TechnicianSettings settings) =>
        {
            var error = TechnicianName.Validate(request.Name);
            if (error is not null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Technician.Name"] = [error],
                });
            }

            settings.SaveLastTechnician(TechnicianName.Normalize(request.Name));
            return Results.NoContent();
        });

        return api;
    }

    public sealed record TechnicianRequest(string? Name);

    public sealed record TechnicianResponse(string? Name);
}
