using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;

namespace Vulcan.Domain.Services;

public static class CompositionValidator
{
    public static decimal CalculateTotal(Composition composition)
    {
        return composition.CementPct
               + composition.ScmPct
               + composition.CoarseAggregatePct
               + composition.FineAggregatePct
               + composition.WaterPct;
    }

    public static decimal CalculateAirOther(Composition composition)
    {
        return 100m - CalculateTotal(composition);
    }

    public static bool IsScmValid(decimal scmPct, ScmType scmType)
    {
        if (scmPct < 0m || scmPct > 100m)
        {
            return false;
        }

        if (scmPct == 0m)
        {
            return scmType == ScmType.None;
        }

        return scmType is ScmType.FlyAsh
            or ScmType.Slag
            or ScmType.SilicaFume;
    }

    public static bool IsTotalValid(Composition composition)
    {
        return CalculateTotal(composition) <= 100m;
    }

    public static bool HasAtMostTwoDecimalPlaces(decimal value)
    {
        return decimal.Round(value, 2) == value;
    }
    // Collect every composition error so all problems can be shown at once.
    public static IReadOnlyList<string> Validate(Composition composition)
    {
        var errors = new List<string>();

        if (!IsScmValid(composition.ScmPct, composition.ScmType))
        {
            errors.Add("SCM percentage and type are inconsistent.");
        }

        if (!HasAtMostTwoDecimalPlaces(composition.CementPct))
        {
            errors.Add("Cement percentage cannot have more than two decimal places.");
        }

        if (!HasAtMostTwoDecimalPlaces(composition.ScmPct))
        {
            errors.Add("SCM percentage cannot have more than two decimal places.");
        }

        if (!HasAtMostTwoDecimalPlaces(composition.CoarseAggregatePct))
        {
            errors.Add("Coarse aggregate percentage cannot have more than two decimal places.");
        }

        if (!HasAtMostTwoDecimalPlaces(composition.FineAggregatePct))
        {
            errors.Add("Fine aggregate percentage cannot have more than two decimal places.");
        }

        if (!HasAtMostTwoDecimalPlaces(composition.WaterPct))
        {
            errors.Add("Water percentage cannot have more than two decimal places.");
        }

        if (!IsTotalValid(composition))
        {
            errors.Add($"Mix percentages total {CalculateTotal(composition)}%, which exceeds 100%.");
        }

        return errors;
    }
}
