
using Microsoft.AspNetCore.Mvc;
using Vulcan.Application.Contracts;
using Vulcan.Application.Interfaces;
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
    BatchExportService batchExportService,
    WaterCementRatioCalculator waterCementRatioCalculator,
    IRecordWarningRepository recordWarningRepository,
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
        [FromQuery] bool? includeSynthetic,
        [FromQuery] int page = 1
    )
    {
        if (page < 1)
        {
            ModelState.AddModelError("page", "Page must be greater than or equal to 1.");
            return ValidationProblem(ModelState);
        }

        var batches = await batchService.SearchAsync(
            search,
            castFrom,
            castTo,
            cementType,
            hasWarnings,
            includeSynthetic,
            page
        );

        var warnings = await recordWarningRepository.GetForBatchesAsync(batches);

        var warningCounts = batches.ToDictionary(
            b => b.Id,
            b => warnings.Count(w =>
                (w.EntityType == nameof(Batch) && w.EntityId == b.Id) ||
                (w.EntityType == nameof(BreakResult) &&
                 b.Specimens.Any(s =>
                     s.BreakResult != null &&
                     s.BreakResult.Id == w.EntityId)) ||
                (w.EntityType == nameof(CuringReading) &&
                 b.Specimens.Any(s =>
                     s.CuringReadings.Any(r => r.Id == w.EntityId)))));

        GetBatchListResponse result = [..batches.Select(b =>
        {
            double? waterCementRatio = null;
            if (b is { Composition: { } composition })
            {
                waterCementRatio = (double?)waterCementRatioCalculator.Calculate(composition);
            }
            return BatchSummaryDTO.FromDomain(
                b,
                waterCementRatio,
                warningCounts.GetValueOrDefault(b.Id));
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

        if (result == null)
        {
            return Results.NotFound();
        }

        double? waterCementRatio = null;

        if (result is { Composition: { } composition })
        {
            waterCementRatio =
                (double?)waterCementRatioCalculator.Calculate(composition);
        }

        var warnings = await recordWarningRepository.GetForBatchesAsync([result]);

        return Results.Json(
            GetBatchResponse.FromDomain(result, waterCementRatio, warnings));
    }

    [HttpPut("{batchNumber}")]
    public async Task<IActionResult> UpdateBatch([FromRoute] string batchNumber, [FromBody] UpdateBatchRequest request)
    {
        var result = await batchService.UpdateAsync(batchNumber, request.ToDomain(batchNumber));
        return HandleBatchResult(result);
    }

    [HttpGet("{batchNumber}/export")]
    public async Task<IActionResult> ExportBatch(
        [FromRoute] string batchNumber)
    {
        var workbook = await batchExportService.ExportAsync(batchNumber);

        if (workbook is null)
        {
            return NotFound();
        }

        var normalizedBatchNumber =
            BatchValidator.NormalizeBatchNumber(batchNumber);

        return File(
            workbook,
            BatchExportService.ContentType,
            $"{normalizedBatchNumber}-export.xlsx");
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

                return ValidationProblem(ModelState);
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
