using Microsoft.AspNetCore.Mvc;
using Vulcan.Application.Services;

namespace Vulcan.Api.Controllers;

[ApiController]
[Route("api/batches")]
public sealed class StrengthController(StrengthService strengthService) : ControllerBase
{
    [HttpGet("{batchNumber}/strength")]
    public async Task<IActionResult> GetStrength(string batchNumber)
    {
        var result = await strengthService.GetByBatchNumberAsync(batchNumber);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }
}
