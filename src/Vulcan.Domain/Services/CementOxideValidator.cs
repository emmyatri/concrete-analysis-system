using Vulcan.Domain.Constants;
using Vulcan.Domain.Validation;

namespace Vulcan.Domain.Services;

public sealed class CementOxideValidator(LimitEvaluator limitEvaluator)
{
    public IReadOnlyDictionary<string, LimitResult> Validate(
        decimal? aluminaPct,
        decimal? ironOxidePct,
        decimal? magnesiumOxidePct,
        decimal? sulfurTrioxidePct)
    {
        return new Dictionary<string, LimitResult>
        {
            [LimitKeys.AluminaPct] = Check(LimitKeys.AluminaPct, "Alumina", aluminaPct),
            [LimitKeys.IronOxidePct] = Check(LimitKeys.IronOxidePct, "Iron oxide", ironOxidePct),
            [LimitKeys.MagnesiumOxidePct] = Check(LimitKeys.MagnesiumOxidePct, "Magnesium oxide", magnesiumOxidePct),
            [LimitKeys.SulfurTrioxidePct] = Check(LimitKeys.SulfurTrioxidePct, "Sulfur trioxide", sulfurTrioxidePct),
        };
    }

    private LimitResult Check(string key, string fieldName, decimal? value)
    {
        if (value is not { } percentage)
        {
            return new LimitResult.Reject($"{fieldName} percent is required");
        }

        if (decimal.Round(percentage, 2) != percentage)
        {
            return new LimitResult.Reject($"{fieldName} percent must have at most two decimal places");
        }

        return limitEvaluator.Check(key, percentage);
    }
}
