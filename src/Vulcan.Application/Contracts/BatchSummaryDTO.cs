using System.ComponentModel.DataAnnotations;
using Vulcan.Domain.Entities;
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

    public static BatchSummaryDTO FromDomain(Batch b, double? waterCementRatio)
        => new()
        {
            Id = b.Id,
            BatchNumber = b.BatchNumber,
            CastAt = b.CastAt,
            CementType = b.CementType,
            ExpectedFinalBreakPsi = b.ExpectedFinalBreakPsi,
            WaterCementRatio = waterCementRatio,
            CylinderCount = b.Specimens.Count,
            BreaksRecorded = b
                .Specimens
                .Sum(s => s.CuringReadings.Count),
            Average28DayPsi = b
                .Specimens
                .Any(s => s.TestAgeDays == TestAgeDays.Day28 && s.BreakResult != null)
                    ? b
                        .Specimens
                        .Where(s => s.TestAgeDays == TestAgeDays.Day28 && s.BreakResult != null)
                        .Average(s => s.BreakResult!.StrengthPsi)
                    : null,
            IsSynthetic = b.IsSynthetic,
            WarningCount = 0, // TODO
        };
}
