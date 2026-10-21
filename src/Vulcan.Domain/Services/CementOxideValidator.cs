using Vulcan.Domain.Constants;
using Vulcan.Domain.Validation;

namespace Vulcan.Domain.Services;

public sealed class CementOxideValidator(LimitEvaluator limitEvaluator)
{
    public ValidationOutcome Validate(
        decimal? aluminaPct,
        decimal? ironOxidePct,
        decimal? magnesiumOxidePct,
        decimal? sulfurTrioxidePct)
    {
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationError>();

        Check(LimitKeys.AluminaPct, "Alumina", aluminaPct, errors, warnings);
        Check(LimitKeys.IronOxidePct, "Iron oxide", ironOxidePct, errors, warnings);
        Check(LimitKeys.MagnesiumOxidePct, "Magnesium oxide", magnesiumOxidePct, errors, warnings);
        Check(LimitKeys.SulfurTrioxidePct, "Sulfur trioxide", sulfurTrioxidePct, errors, warnings);

        return new ValidationOutcome(errors, warnings);
    }

    private void Check(
        string key,
        string fieldName,
        decimal? value,
        List<ValidationError> errors,
        List<ValidationError> warnings)
    {
        if (value is not { } percentage)
        {
            errors.Add(new ValidationError(key, $"{fieldName} percent is required"));
            return;
        }

        if (decimal.Round(percentage, 2) != percentage)
        {
            errors.Add(new ValidationError(key, $"{fieldName} percent must have at most two decimal places"));
            return;
        }

        switch (limitEvaluator.Check(key, percentage))
        {
            case LimitResult.Reject reject:
                errors.Add(new ValidationError(key, reject.Message));
                break;
            case LimitResult.Warn warn:
                warnings.Add(new ValidationError(key, warn.Message));
                break;
        }
    }
}
