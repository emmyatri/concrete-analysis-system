using System.Text.RegularExpressions;
using Vulcan.Domain.Constants;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Validation;

namespace Vulcan.Domain.Services;

public partial class BatchValidator(LimitEvaluator limitEvaluator)
{
    public static string NormalizeBatchNumber(string? batchNumber)
    {
        return (batchNumber ?? string.Empty).Trim();
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

    public bool IsFinalWeightValid(decimal finalWeightLb)
    {
        var result = limitEvaluator.Check(
            LimitKeys.FinalWeightLb,
            finalWeightLb);

        if (result is LimitResult.Reject)
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

    public bool IsExpectedFinalBreakPsiValid(int expectedFinalBreakPsi)
    {
        var result = limitEvaluator.Check(
            LimitKeys.ExpectedFinalBreakPsi,
            expectedFinalBreakPsi);

        return result is not LimitResult.Reject;
    }

    /// <summary>
    /// Mix water pH (FR-7): Reject outside 1–9, Warn outside the normal 7.0–7.5.
    /// A Warn still saves and is flagged.
    /// </summary>
    public LimitResult CheckMixWaterPh(decimal mixWaterPh)
    {
        return limitEvaluator.Check(LimitKeys.MixWaterPh, mixWaterPh);
    }

    [GeneratedRegex(@"^[A-Za-z0-9-]+$")]
    private static partial Regex BatchNumberPattern();
}
