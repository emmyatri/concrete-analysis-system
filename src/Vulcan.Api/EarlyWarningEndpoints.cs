using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vulcan.Application.Contracts;
using Vulcan.Application.Interfaces;
using Vulcan.Application.Services;

namespace Vulcan.Api;

public static class EarlyWarningEndpoints
{
    public static void MapEarlyWarningEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/warnings/early", async (
            [FromServices] EarlyWarningQuery query,
            CancellationToken cancellationToken,
            [FromQuery] bool includeSynthetic = true) =>
                Results.Ok(await query.GetAsync(includeSynthetic, cancellationToken)))
            .Produces<IReadOnlyList<EarlyWarningBatchDTO>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
