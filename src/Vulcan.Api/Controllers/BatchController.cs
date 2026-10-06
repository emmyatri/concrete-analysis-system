using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Vulcan.Domain.Entities;

namespace Vulcan.Api.Controllers;

[ApiController]
[Route("/api/batches")]
class BatchController(
    BatchService batchService,
    ILogger<BatchController> logger
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetBatchList([FromQuery] int q, [FromQuery] int page)
    {
        var result = batchService.SearchAsync(q, page);
        return result;
    }

    [HttpPost]
    public async Task<IActionResult> CreateBatch([FromBody] CreateBatchRequest request)
    {
        var existing = await batchService.GetByNumberAsync(request.batchNumber);
        if (existing)
        {
            return (IActionResult)Results.Conflict();
        }

        var result = await batchService.CreateAsync(batch);
        if (result is { ServiceResult<Batch>.Failure } errors)
        {
            foreach (error in errors)
            {
                ModelState.AddModelError(error.Key, error.Message);
            }
            return ValidationProblem(ModelState);
        }

        return (IActionResult)Results.Json(new { }, statusCode: 201);
    }

    [HttpGet("{batchNumber}")]
    public async Task<IResult> GetBatch([FromRoute] string batchNumber)
    {
        var result = await batchService.GetByNumberAsync(batchNumber);

        if (result == null)
        {
            return Results.NotFound();
        }

        return Results.Json<Batch>(result);
    }

    [HttpPut("{batchNumber}")]
    public async Task<IActionResult> UpdateBatch([FromRoute] string batchNumber, [FromBody] UpdateBatchRequest request)
    {
        var existing = await batchService.GetByNumberAsync(batchNumber);
        if (!existing)
        {
            return Results.NotFound();
        }

        var result = await batchService.UpdateAsync(batchNumber, batch);
        if (result is ServiceResult<Batch>.Failure errors)
        {
            foreach (error in errors)
            {
                ModelState.AddModelError(error.Key, error.Message);
            }
            return ValidationProblem(ModelState);
        }

        return (IActionResult)Results.Json(new { }, statusCode: 200);
    }
}
