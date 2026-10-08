using Microsoft.AspNetCore.Mvc;
using Vulcan.Application.Contracts;
using Vulcan.Application.Services;
using Vulcan.Domain.Entities;

namespace Vulcan.Api.Controllers;

[ApiController]
[Route("/api/specific-gravities")]
public class SpecificGravityController(SpecificGravityService specificGravityService) : ControllerBase
{
    [HttpGet]
    public async Task<IResult> GetSpecificGravities()
    {
        var gravities = await specificGravityService.GetSpecificGravitiesAsync();
        return Results.Json(gravities.Select(g => new SpecificGravityDTO
        {
            Material = g.Material,
            Value = (double)g.Value
        }));
    }
}
