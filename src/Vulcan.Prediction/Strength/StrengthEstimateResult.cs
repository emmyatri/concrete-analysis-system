namespace Vulcan.Prediction.Strength;

public sealed record StrengthEstimateResult
{
    public required IReadOnlyList<StrengthCurvePoint> DailyCurve { get; init; }

    public StrengthDayEstimate? Day3 { get; init; }

    public StrengthDayEstimate? Day7 { get; init; }

    public StrengthDayEstimate? Day28 { get; init; }

    public required string Basis { get; init; }

    public required string CalibrationVersion { get; init; }

    public string? StatusMessage { get; init; }
}
