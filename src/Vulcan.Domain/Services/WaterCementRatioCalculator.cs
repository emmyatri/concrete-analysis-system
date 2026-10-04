using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Interfaces;
using Vulcan.Domain.Validation;

namespace Vulcan.Domain.Services;

public class WaterCementRatioCalculator(
    ISpecificGravityProvider specificGravityProvider,
    LimitEvaluator limitEvaluator)
{
    public decimal? Calculate(Composition composition)
    {
        // Client Decision 1: mix percentages are entered by volume, while the
        // water-to-cement ratio is defined by weight. Specific gravity converts
        // each volume percentage to its relative weight contribution.
        var waterWeight =
            composition.WaterPct *
            specificGravityProvider.Get(Material.Water);

        var cementWeight =
            composition.CementPct *
            specificGravityProvider.Get(Material.Cement);

        var scmWeight = composition.ScmPct == 0m
            ? 0m
            : composition.ScmPct *
              specificGravityProvider.Get(GetScmMaterial(composition.ScmType));

        var cementitiousWeight = cementWeight + scmWeight;

        if (cementitiousWeight == 0m)
        {
            return null;
        }

        return waterWeight / cementitiousWeight;
    }

    public LimitResult? Evaluate(decimal? ratio)
    {
        if (ratio is null)
        {
            return null;
        }

        return limitEvaluator.Check(
            LimitKeys.WaterCementRatio,
            ratio.Value);
    }

    private static Material GetScmMaterial(ScmType scmType)
    {
        return scmType switch
        {
            ScmType.FlyAsh => Material.FlyAsh,
            ScmType.Slag => Material.Slag,
            ScmType.SilicaFume => Material.SilicaFume,
            _ => throw new ArgumentOutOfRangeException(
                nameof(scmType),
                scmType,
                "SCM type must identify a supported SCM material.")
        };
    }
}
