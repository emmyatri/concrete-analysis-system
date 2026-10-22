namespace Vulcan.Prediction.Adjustments;

public sealed record BreakAdjustmentResult(
    BreakAdjustmentStage Stage,
    double UnadjustedPsi,
    double AdjustedPsi,
    double AdjustedA,
    double ReferenceStrengthPsi,
    string? FallbackReason
);
