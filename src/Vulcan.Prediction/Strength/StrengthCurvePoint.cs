namespace Vulcan.Prediction.Strength;

public sealed record StrengthCurvePoint(
    int Day,
    double EquivalentAgeDays,
    double EstimatedPsi
);
