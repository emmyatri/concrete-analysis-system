using System.ComponentModel.DataAnnotations;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;

namespace Vulcan.Application.Contracts;

public class UpdateBatchRequest
{
    [Required]
    public required DateTime CastAt { get; init; }

    [Required]
    public required double FinalWeightLb { get; init; }

    [Required]
    public required CementType CementType { get; init; }

    [Required]
    public required double MixWaterPh { get; init; }

    public bool IsSynthetic { get; init; } = false;

    [Required]
    public required int ExpectedFinalBreakPsi { get; init; }

    public CompositionDto? Composition { get; init; }

    public OxidesDto? Oxides { get; init; }

    public Batch ToDomain(string batchNumber)
        => new()
        {
            BatchNumber = batchNumber,
            CastAt = CastAt,
            FinalWeightLb = (decimal)FinalWeightLb,
            CementType = CementType,
            MixWaterPh = (decimal)MixWaterPh,
            IsSynthetic = IsSynthetic,
            ExpectedFinalBreakPsi = ExpectedFinalBreakPsi,
            Composition = Composition?.ToDomain(),
            CementOxideAnalysis = Oxides?.ToDomain(),
        };
}
