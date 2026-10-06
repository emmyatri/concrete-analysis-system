using System.Net.Security;
using Microsoft.AspNetCore.Mvc;
using Vulcan.Application.Services;
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
    public async Task<IResult> GetBatchList([FromQuery] string q, [FromQuery] int page)
    {
        var result = await batchService.SearchAsync(q, page);
        return Results.Json(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBatch([FromBody] CreateBatchRequest request)
    {
        var result = await batchService.CreateAsync(request.batch);
        return HandleBatchResult(result, successStatusCode: StatusCodes.Status201Created);
    }

    [HttpGet("{batchNumber}")]
    public async Task<IResult> GetBatch([FromRoute] string batchNumber)
    {
        var result = await batchService.GetByNumberAsync(batchNumber);
        return result == null
            ? Results.NotFound()
            : Results.Json(result);
    }

    [HttpPut("{batchNumber}")]
    public async Task<IActionResult> UpdateBatch([FromRoute] string batchNumber, [FromBody] UpdateBatchRequest request)
    {
        var result = await batchService.UpdateAsync(batchNumber, Batch.FromDTO(request));
        return HandleBatchResult(result);
    }

    private IActionResult HandleBatchResult(ServiceResult<Batch> batchResult,
        int successStatusCode = StatusCodes.Status200OK)
    {
        switch (batchResult)
        {
            case { Kind: ServiceResultKind.Invalid, Errors: var errors }:
                foreach (var error in errors)
                {
                    ModelState.AddModelError(error.Key, error.Message);
                }

                return ValidationProblem(ModelState);

            case { Kind: ServiceResultKind.Success, Value: { } batch, Warnings: var warnings }:
                return (IActionResult)Results.Json(new
                {
                    id = batch.Id,
                    batchNumber = batch.BatchNumber,
                    warnings,
                }, statusCode: successStatusCode);

            case { Kind: ServiceResultKind.NotFound, Errors: var errors}:
                foreach (var error in errors)
                {
                    ModelState.AddModelError(error.Key, error.Message);
                }

                return ValidationProblem(
                    modelStateDictionary: ModelState,
                    statusCode: StatusCodes.Status404NotFound);

            case { Kind: ServiceResultKind.Conflict, Errors: var errors }:
                foreach (var error in errors)
                {
                    ModelState.AddModelError(error.Key, error.Message);
                }

                return ValidationProblem(
                    modelStateDictionary: ModelState,
                    statusCode: StatusCodes.Status409Conflict);
        }

        logger.LogError("Batch request failed with unknown error.");
        return (IActionResult)Results.InternalServerError();
    }
}
