using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using Vulcan.Application.Contracts;
using Vulcan.Application.Services;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Services;
using Vulcan.Domain.Validation;

namespace Vulcan.Api.Controllers;

[ApiController]
[Route("/api/batches")]
public class BatchController(
    BatchService batchService,
    WaterCementRatioCalculator waterCementRatioCalculator,
    ILogger<BatchController> logger
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetBatchList(
        [FromQuery] string? search,
        [FromQuery] DateTime? castFrom,
        [FromQuery] DateTime? castTo,
        [FromQuery] CementType? cementType,
        [FromQuery] bool? hasWarnings,
        [FromQuery] bool? includeSynthetic
    )
    {
        var batches = await batchService.SearchAsync(
            search,
            castFrom,
            castTo,
            cementType,
            hasWarnings,
            includeSynthetic
        );
        GetBatchListResponse result = [..batches.Select(b =>
        {
            double? waterCementRatio = null;
            if (b is { Composition: { } composition })
            {
                waterCementRatio = (double?)waterCementRatioCalculator.Calculate(composition);
            }
            return BatchSummaryDTO.FromDomain(b, waterCementRatio);
        })];
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBatch([FromBody] CreateBatchRequest request)
    {
        var result = await batchService.CreateAsync(request.ToDomain());
        return HandleBatchResult(result, successStatusCode: StatusCodes.Status201Created);
    }

    [HttpGet("{batchNumber}")]
    public async Task<IResult> GetBatch([FromRoute] string batchNumber)
    {
        var result = await batchService.GetByNumberAsync(batchNumber);
        double? waterCementRatio = null;
        if (result is { Composition: { } composition })
        {
            waterCementRatio = (double?)waterCementRatioCalculator.Calculate(composition);
        }
        return result == null
            ? Results.NotFound()
            : Results.Json(GetBatchResponse.FromDomain(result, waterCementRatio));
    }

    [HttpPut("{batchNumber}")]
    public async Task<IActionResult> UpdateBatch([FromRoute] string batchNumber, [FromBody] UpdateBatchRequest request)
    {
        var result = await batchService.UpdateAsync(batchNumber, request.ToDomain(batchNumber));
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
                return StatusCode(statusCode: successStatusCode, new CreateBatchResponse
                {
                    Id = batch.Id,
                    BatchNumber = batch.BatchNumber,
                    Warnings = ToWarningDto(warnings),
                });

            case { Kind: ServiceResultKind.NotFound, Errors: var errors }:
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
        return StatusCode(StatusCodes.Status500InternalServerError);
    }

    private static List<WarningDTO> ToWarningDto(IEnumerable<ValidationError>? items)
        => [.. items?.Select(i => new WarningDTO
        {
            Key = i.Key,
            Message = i.Message
        }) ?? []];
}
