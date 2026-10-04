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
    public IReadOnlyList<ValidationError> Validate(Composition composition)
    {
        var errors = new List<ValidationError>();

        AddLimitError(errors, LimitKeys.CementPct, composition.CementPct);
        AddLimitError(errors, LimitKeys.FineAggregatePct, composition.FineAggregatePct);
        AddLimitError(errors, LimitKeys.CoarseAggregatePct, composition.CoarseAggregatePct);
        AddLimitError(errors, LimitKeys.WaterPct, composition.WaterPct);

        if (!IsScmValid(composition.ScmPct, composition.ScmType))
        {
            errors.Add(new ValidationError(
                LimitKeys.ScmPct,
                "SCM percentage and type are inconsistent."));
        }

        AddPrecisionError(errors, LimitKeys.CementPct, composition.CementPct, "Cement");
        AddPrecisionError(errors, LimitKeys.ScmPct, composition.ScmPct, "SCM");
        AddPrecisionError(errors, LimitKeys.CoarseAggregatePct, composition.CoarseAggregatePct, "Coarse aggregate");
        AddPrecisionError(errors, LimitKeys.FineAggregatePct, composition.FineAggregatePct, "Fine aggregate");
        AddPrecisionError(errors, LimitKeys.WaterPct, composition.WaterPct, "Water");

        var total = CalculateTotal(composition);
        if (limitEvaluator.Check(LimitKeys.TotalPct, total) is LimitResult.Reject)
        {
            errors.Add(new ValidationError(
                LimitKeys.TotalPct,
                $"Mix percentages total {total}%, which exceeds 100%."));
        }

        return errors;
    }

    private void AddLimitError(List<ValidationError> errors, string key, decimal value)
    {
        if (limitEvaluator.Check(key, value) is LimitResult.Reject reject)
        {
            errors.Add(new ValidationError(
                    key,
                    reject.Message));
        }
    }

    private static void AddPrecisionError(
        List<ValidationError> errors,
        string key,
        decimal value,
        string fieldName)
    {
        if (!HasAtMostTwoDecimalPlaces(value))
        {
            errors.Add(new ValidationError(
                key,
                $"{fieldName} percentage cannot have more than two decimal places."));
        }
    }
}
