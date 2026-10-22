using Vulcan.Prediction.Adjustments;

namespace Vulcan.Prediction.Strength;

public sealed record StrengthEstimateResult
{
    public required IReadOnlyList<StrengthCurvePoint> DailyCurve { get; init; }

    public StrengthDayEstimate? Day3 { get; init; }

    public StrengthDayEstimate? Day7 { get; init; }

    public StrengthDayEstimate? Day28 { get; init; }
    
    // Break-adjusted curve using all currently recorded measurements.
    public IReadOnlyList<StrengthCurvePoint>? AdjustedDailyCurve { get; init; }

    // Adjustment results for each prediction horizon.
    // Includes original PSI, adjusted PSI, stage, and fallback reason.
    public BreakAdjustmentResult? Day3Adjustment { get; init; }

    public BreakAdjustmentResult? Day7Adjustment { get; init; }

    public BreakAdjustmentResult? Day28Adjustment { get; init; }

    public required string Basis { get; init; }

    public required string CalibrationVersion { get; init; }

    public string? StatusMessage { get; init; }
}
