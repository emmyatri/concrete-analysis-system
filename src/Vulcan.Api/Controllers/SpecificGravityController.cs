using Microsoft.AspNetCore.Mvc;
using Vulcan.Application.Services;

namespace Vulcan.Api.Controllers;

[ApiController]
[Route("/api/specific-gravities")]
public class SpecificGravityController(SpecificGravityService specificGravityService) : ControllerBase
{
    [HttpGet]
    public async Task<IResult> GetSpecificGravities()
    {
        return Results.Json(
            await specificGravityService.GetSpecificGravitiesAsync());
    }
}
