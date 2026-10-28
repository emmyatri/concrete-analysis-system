using System.ComponentModel.DataAnnotations;
using Vulcan.Domain.Enums;

namespace Vulcan.Application.Contracts;

public class StrengthEstimateDTO
{
    [Required]
    public required int HorizonDays { get; init; }

    public int? EstimatedPsi { get; init; }

    public double? EquivalentAgeDays { get; init; }

    public AdjustmentStage? AdjustmentStage { get; init; }

    public int? UnadjustedPsi { get; init; }

    [Required]
    public required string Basis { get; init; }

    [Required]
    public required string CalibratedVersion { get; init; }

    [Required]
    public required bool IsSynthetic { get; init; }

    [Required]
    public required DateTime CreatedAtUtc { get; init; }

    [Required]
    public required string CreatedBy { get; init; }

    public DateTime? ModifiedAtUtc { get; init; }

    public string? ModifiedBy { get; init; }
}
