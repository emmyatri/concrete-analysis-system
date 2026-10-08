using System.ComponentModel.DataAnnotations;
using Vulcan.Domain.Enums;

namespace Vulcan.Application.Contracts;

public class CompositionDtoResponse
{
    [Required]
    public required double CementPct { get; init; }

    [Required]
    public required double WaterPct { get; init; }

    [Required]
    public required double CoarseAggregatePct { get; init; }

    [Required]
    public required double FineAggregatePct { get; init; }

    [Required]
    public required ScmType ScmType { get; init; }

    [Required]
    public required double ScmPct { get; init; }

    // derived attributes
    [Required]
    public required double TotalPct { get; init; }

    [Required]
    public required double AirOtherPct { get; init; }

    public double? WaterCementRatio { get; init; }
}
