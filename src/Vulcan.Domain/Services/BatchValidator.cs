using System.Text.RegularExpressions;

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
            return false;

        if (normalized.Length > 32)
            return false;

        return BatchNumberPattern().IsMatch(normalized);
    }
    
    public static bool IsFinalWeightValid(decimal finalWeightLb)
    {
        if (finalWeightLb <= 0)
            return false;

        return decimal.Round(finalWeightLb, 2) == finalWeightLb;
    }

    [GeneratedRegex(@"^[A-Za-z0-9-]+$")]
    private static partial Regex BatchNumberPattern();
}
