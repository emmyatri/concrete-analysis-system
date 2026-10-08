using Microsoft.AspNetCore.Mvc;
using Vulcan.Application.Contracts;
using Vulcan.Application.Services;

namespace Vulcan.Api.Controllers;

[ApiController]
[Route("/api/validation-limits")]
public class ValidationLimitController(ValidationLimitService validationLimitService) : ControllerBase
{
    [HttpGet]
    public async Task<IResult> GetValidationLimits()
    {
        var limits = await validationLimitService.GetValidationLimitsAsync();
        return Results.Json(limits.Select(l =>
            new ValidationLimitDTO
            {
                Key = l.Key,
                Severity = l.Severity.ToString(),
                Unit = l.Unit,
                Message = l.Message,
                Source = l.Source,
                Min = (double?)(l?.Min),
                Max = (double?)(l?.Max),
            }));
    }
}
