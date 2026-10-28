using System.ComponentModel.DataAnnotations;
using Vulcan.Domain.Enums;

namespace Vulcan.Application.Contracts;

public class GetBatchStrengthResponse
{
    [Required]
    public required int Id { get; init; }

    [Required]
    public required string BatchNumber { get; init; }

    [Required]
    public required bool IsSynthetic { get; init; }

    [Required]
    public required int ExpectedFinalBreakPsi { get; init; }

    [Required]
    public required CementType CementType { get; init; }

    [Required]
    public required string Status { get; init; }

    [Required]
    public required bool AtRisk { get; init; }

    public int? Projected28DayPsi { get; init; }

    public int? ShortfallPsi { get; init; }

    public double? ShortfallPct { get; init; }

    [Required]
    public required List<StrengthCurvePointDTO> CurvePoints { get; init; } = [];

    [Required]
    public required List<StrengthCurvePointDTO> UnadjustedCurvePoints { get; init; } = [];

    [Required]
    public required List<StrengthEstimateDTO> Estimates { get; init; } = [];

    [Required]
    public required List<MeasuredBreakDTO> MeasuredBreaks { get; init; } = [];

    [Required]
    public required List<EarlyWarningDTO> EarlyWarnings { get; init; } = [];

    [Required]
    public required List<CuringPointDTO> CuringPoints { get; init; } = [];
}
