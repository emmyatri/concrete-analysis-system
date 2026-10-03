using System.Text.RegularExpressions;
using Vulcan.Domain.Enums;

namespace Vulcan.Domain.Services;

public static partial class BatchValidator
{
    public static string NormalizeBatchNumber(string? batchNumber)
    {
        return batchNumber?.Trim() ?? string.Empty;
    }

    public static bool IsBatchNumberValid(string? batchNumber)
    {
        var normalized = NormalizeBatchNumber(batchNumber);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (normalized.Length > 32)
        {
            return false;
        }

        return BatchNumberPattern().IsMatch(normalized);
    }

    public static bool IsFinalWeightValid(decimal finalWeightLb)
    {
        if (finalWeightLb <= 0)
        {
            return false;
        }

        return decimal.Round(finalWeightLb, 2) == finalWeightLb;
    }

    public static bool IsCastAtValid(DateTime castAt, DateTime nowUtc)
    {
        if (castAt == default)
        {
            return false;
        }

        return castAt <= nowUtc;
    }

    public static bool IsCementTypeValid(CementType cementType)
    {
        return cementType is CementType.TypeI
            or CementType.TypeII
            or CementType.TypeIII;
    }

    [GeneratedRegex(@"^[A-Za-z0-9-]+$")]
    private static partial Regex BatchNumberPattern();
}
