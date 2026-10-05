using Microsoft.AspNetCore.Mvc;

namespace Vulcan.Api.Controllers;

[ApiController]
[Route("/api/batches")]
class BatchController(
    IBatchService batchService,
    ILogger<BatchController> logger
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetBatchList([FromQuery] int q, [FromQuery] int page)
    {

    }

    [HttpPost("{batchNumber}")]
    public async Task<IActionResult> CreateBatch([FromRoute] string batchNumber)
    {

    }

    [HttpGet("{batchNumber}")]
    public async Task<IActionResult> GetBatch([FromRoute] string batchNumber)
    {

    }

    [HttpPut("{batchNumber}")]
    public async Task<IActionResult> UpdateBatch([FromRoute] string batchNumber)
    {

    }
}