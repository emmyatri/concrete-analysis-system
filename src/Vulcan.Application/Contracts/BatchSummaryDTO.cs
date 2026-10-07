using System.ComponentModel.DataAnnotations;
using Vulcan.Domain.Enums;

namespace Vulcan.Application.Contracts;

public class BatchSummaryDTO
{
    [Required]
    public required int Id { get; init; }

    [Required]
    public required string BatchNumber { get; init; }

    [Required]
    public required DateTime CastAt { get; init; }

    [Required]
    public required CementType CementType { get; init; }

    [Required]
    public required int ExpectedFinalBreakPsi { get; init; }

    public double? WaterCementRatio { get; init; }

    [Required]
    public required int CylinderCount { get; init; }

    [Required]
    public required int BreaksRecorded { get; init; }

    public double? Average28DayPsi { get; init; }

    [Required]
    public required int WarningCount { get; init; }

    [Required]
    public required bool IsSynthetic { get; init; }
}
