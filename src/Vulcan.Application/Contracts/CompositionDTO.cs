using System.ComponentModel.DataAnnotations;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;

namespace Vulcan.Application.Contracts;

public class CompositionDto
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

    // no derived attributes

    public Composition ToDomain()
        => new()
        {
            CementPct = (decimal)CementPct,
            WaterPct = (decimal)WaterPct,
            CoarseAggregatePct = (decimal)CoarseAggregatePct,
            FineAggregatePct = (decimal)FineAggregatePct,
            ScmType = ScmType,
            ScmPct = (decimal)ScmPct
        };
}
