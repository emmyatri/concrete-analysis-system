using System.ComponentModel.DataAnnotations;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;

namespace Vulcan.Application.Contracts;

public class GetBatchResponse
{
    [Required]
    public required string BatchNumber { get; init; }

    [Required]
    public required DateTime CastAt { get; init; }

    [Required]
    public required double FinalWeightLb { get; init; }

    [Required]
    public required CementType CementType { get; init; }

    [Required]
    public required double MixWaterPh { get; init; }

    [Required]
    public required bool IsSynthetic { get; init; }

    public CompositionDto? Composition { get; init; }

    public OxidesDto? Oxides { get; init; }

    public List<Specimen> Specimens { get; init; } = [];
}
