using Microsoft.AspNetCore.Mvc;
using Vulcan.Application.Contracts;
using Vulcan.Application.Services;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Services;
using Vulcan.Domain.Validation;

namespace Vulcan.Api.Controllers;

/// <summary>
/// Cylinders, breaks and curing readings (SCRUM-121). Maps requests to the services and results to
/// status codes. No business rules here: validation lives in Vulcan.Domain and the services.
/// </summary>
[ApiController]
[Route("api")]
public class SpecimenController(
    SpecimenService specimenService,
    CuringService curingService) : ControllerBase
{
    [HttpPost("batches/{batchNumber}/specimens")]
    public async Task<IActionResult> CreateSpecimen(
        string batchNumber,
        CreateSpecimenRequest request)
    {
        var specimen = new Specimen
        {
            Label = request.Label,
            LengthIn = (decimal)request.LengthIn,
            WidthIn = (decimal)request.WidthIn,
            TestAgeDays = request.TestAgeDays,
        };

        var result = await specimenService.AddAsync(batchNumber, specimen);

        if (!result.IsSuccess)
        {
            // A label already used on this batch is a bad value on the form (400), per docs/api.md.
            return ErrorResponse(result, conflictStatus: StatusCodes.Status400BadRequest);
        }

        return StatusCode(StatusCodes.Status201Created, new CreateSpecimenResponse
        {
            Id = result.Value!.Id,
            Label = result.Value.Label,
            batchNumber = BatchValidator.NormalizeBatchNumber(batchNumber),
            Warnings = ToWarnings(result.Warnings),
        });
    }

    [HttpPost("specimens/{specimenId:int}/break")]
    public async Task<IActionResult> CreateBreakResult(
        int specimenId,
        CreateBreakResultRequest request)
    {
        var breakResult = new BreakResult
        {
            BrokenAt = request.BrokenAt,
            StrengthPsi = request.StrengthPsi,
        };

        var result = await specimenService.RecordBreakAsync(specimenId, breakResult);

        if (!result.IsSuccess)
        {
            // A cylinder can only be broken once: a second result is a 409.
            return ErrorResponse(result, conflictStatus: StatusCodes.Status409Conflict);
        }

        return StatusCode(StatusCodes.Status201Created, new CreateBreakResultResponse
        {
            Id = result.Value!.Id,
            SpecimenId = specimenId,
            Warnings = ToWarnings(result.Warnings),
        });
    }

    [HttpPost("specimens/{specimenId:int}/readings")]
    public async Task<IActionResult> CreateCuringReading(
        int specimenId,
        CreateCuringReadingRequest request)
    {
        var reading = new CuringReading
        {
            TakenAt = request.TakenAt,
            InternalTempF = (decimal)request.InternalTempF,
            AmbientTempF = (decimal)request.AmbientTempF,
            MoisturePct = (decimal)request.MoisturePct,
            RelativeHumidityPct = (decimal)request.RelativeHumidityPct,
            ResistivityOhmPerFt = (decimal)request.ResistivityOhmPerFt,
            Co2Ppm = (decimal)request.Co2Ppm,
        };

        var result = await curingService.AddReadingAsync(specimenId, reading);

        if (!result.IsSuccess)
        {
            return ErrorResponse(result, conflictStatus: StatusCodes.Status409Conflict);
        }

        return StatusCode(StatusCodes.Status201Created, new CreateCuringReadingResponse
        {
            Id = result.Value!.Id,
            SpecimenId = specimenId,
            Warnings = ToWarnings(result.Warnings),
        });
    }

    /// <summary>Cylinders without a break yet, soonest due first (Break results screen).</summary>
    [HttpGet("breaks/pending")]
    public async Task<IActionResult> GetPendingBreaks(
        [FromQuery] bool includeSynthetic = true)
    {
        var pending = await specimenService.GetPendingBreaksAsync(includeSynthetic);

        return Ok(pending.Select(specimen => new PendingBreakDTO
        {
            SpecimenId = specimen.Id,
            BatchNumber = specimen.Batch.BatchNumber,
            Label = specimen.Label,
            TestAgeDays = specimen.TestAgeDays,
            CastAt = specimen.Batch.CastAt,
            IsSynthetic = specimen.IsSynthetic,
        }));
    }

    /// <summary>
    /// Turns a failed service result into ValidationProblemDetails, keyed by ValidationLimit key, so the UI
    /// can show each message next to its field. 400 for bad values, 404 for a missing batch or cylinder.
    /// </summary>
    private ActionResult ErrorResponse<T>(ServiceResult<T> result, int conflictStatus)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(error.Key, error.Message);
        }

        var status = result.Kind switch
        {
            ServiceResultKind.NotFound => StatusCodes.Status404NotFound,
            ServiceResultKind.Conflict => conflictStatus,
            _ => StatusCodes.Status400BadRequest,
        };

        return ValidationProblem(statusCode: status, modelStateDictionary: ModelState);
    }

    private static List<WarningDTO> ToWarnings(IReadOnlyList<ValidationError> warnings) =>
        warnings
            .Select(warning => new WarningDTO { Key = warning.Key, Message = warning.Message })
            .ToList();
}
