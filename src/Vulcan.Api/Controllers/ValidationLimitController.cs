using Microsoft.AspNetCore.Mvc;
using Vulcan.Application.Services;

namespace Vulcan.Api.Controllers;

[ApiController]
[Route("/api/validation-limits")]
public class ValidationLimitController(ValidationLimitService validationLimitService) : ControllerBase
{
    [HttpGet]
    public async Task<IResult> GetValidationLimits()
    {
        return Results.Json(await validationLimitService.GetValidationLimitsAsync());
    }
}
