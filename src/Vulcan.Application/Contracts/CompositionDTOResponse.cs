using System.ComponentModel.DataAnnotations;
using Vulcan.Domain.Entities;
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

    public static CompositionDtoResponse FromDomain(Composition c, double? waterCementRatio)
    {
        double totalPct = (double)(c.CementPct + c.ScmPct + c.CoarseAggregatePct + c.FineAggregatePct + c.WaterPct);
        double airOtherPct = 100 - totalPct;

        return new CompositionDtoResponse
        {
            CementPct = (double)c.CementPct,
            WaterPct = (double)c.WaterPct,
            CoarseAggregatePct = (double)c.CoarseAggregatePct,
            FineAggregatePct = (double)c.FineAggregatePct,
            ScmType = c.ScmType,
            ScmPct = (double)c.ScmPct,

            // derived
            TotalPct = totalPct,
            AirOtherPct = airOtherPct,
            WaterCementRatio = waterCementRatio,
        };
    }
}
