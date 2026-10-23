using Vulcan.Prediction.Adjustments;

namespace Vulcan.Prediction.Strength;

public sealed record StrengthEstimateResult
{
    /// <summary>Original, unadjusted daily strength predictions.</summary>
    public required IReadOnlyList<StrengthCurvePoint> DailyCurve { get; init; }

    /// <summary>Original, unadjusted 3-day strength estimate.</summary>
    public StrengthDayEstimate? Day3 { get; init; }

    /// <summary>Original, unadjusted 7-day strength estimate.</summary>
    public StrengthDayEstimate? Day7 { get; init; }

    /// <summary>Original, unadjusted 28-day strength estimate.</summary>
    public StrengthDayEstimate? Day28 { get; init; }

    /// <summary>Daily strength predictions after applying available break adjustments.</summary>
    public IReadOnlyList<StrengthCurvePoint>? AdjustedDailyCurve { get; init; }

    /// <summary>3-day adjustment result, including stage, PSI values, and fallback reason.</summary>
    public BreakAdjustmentResult? Day3Adjustment { get; init; }

    /// <summary>7-day adjustment result, including stage, PSI values, and fallback reason.</summary>
    public BreakAdjustmentResult? Day7Adjustment { get; init; }

    /// <summary>28-day adjustment result, including stage, PSI values, and fallback reason.</summary>
    public BreakAdjustmentResult? Day28Adjustment { get; init; }

    public required string Basis { get; init; }

    public required string CalibrationVersion { get; init; }

    public string? StatusMessage { get; init; }
}
