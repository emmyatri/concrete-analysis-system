namespace Vulcan.Prediction.Adjustments;

public sealed record BreakAdjustmentResult(
    BreakAdjustmentStage Stage,
    double UnadjustedPsi,
    double AdjustedPsi,
    double AdjustedA,
    double ReferenceStrengthPsi,
    string? FallbackReason
)
{
    /// <summary>Explains how the adjusted strength prediction was calculated.</summary>
    public string? Basis { get; init; }
}
