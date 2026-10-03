using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Validation;

namespace Vulcan.Domain.Services;

public class CompositionValidator(LimitEvaluator limitEvaluator)
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

    public bool IsScmValid(decimal scmPct, ScmType scmType)
    {
        if (limitEvaluator.Check(LimitKeys.ScmPct, scmPct) is LimitResult.Reject)
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

    public bool IsTotalValid(Composition composition)
    {
        return limitEvaluator.Check(
            LimitKeys.TotalPct,
            CalculateTotal(composition)) is not LimitResult.Reject;
    }

    public static bool HasAtMostTwoDecimalPlaces(decimal value)
    {
        return decimal.Round(value, 2) == value;
    }

    // Collect every composition error so all problems can be shown at once.
    public IReadOnlyList<string> Validate(Composition composition)
    {
        var errors = new List<string>();

        AddLimitError(errors, LimitKeys.CementPct, composition.CementPct);
        AddLimitError(errors, LimitKeys.FineAggregatePct, composition.FineAggregatePct);
        AddLimitError(errors, LimitKeys.CoarseAggregatePct, composition.CoarseAggregatePct);
        AddLimitError(errors, LimitKeys.WaterPct, composition.WaterPct);

        if (!IsScmValid(composition.ScmPct, composition.ScmType))
        {
            errors.Add("SCM percentage and type are inconsistent.");
        }

        AddPrecisionError(errors, composition.CementPct, "Cement");
        AddPrecisionError(errors, composition.ScmPct, "SCM");
        AddPrecisionError(errors, composition.CoarseAggregatePct, "Coarse aggregate");
        AddPrecisionError(errors, composition.FineAggregatePct, "Fine aggregate");
        AddPrecisionError(errors, composition.WaterPct, "Water");

        var total = CalculateTotal(composition);
        if (limitEvaluator.Check(LimitKeys.TotalPct, total) is LimitResult.Reject)
        {
            errors.Add($"Mix percentages total {total}%, which exceeds 100%.");
        }

        return errors;
    }

    private void AddLimitError(List<string> errors, string key, decimal value)
    {
        if (limitEvaluator.Check(key, value) is LimitResult.Reject reject)
        {
            errors.Add(reject.Message);
        }
    }

    private static void AddPrecisionError(
        List<string> errors,
        decimal value,
        string fieldName)
    {
        if (!HasAtMostTwoDecimalPlaces(value))
        {
            errors.Add($"{fieldName} percentage cannot have more than two decimal places.");
        }
    }
}
