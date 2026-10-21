namespace Vulcan.Prediction.Strength;

public sealed record StrengthDayEstimate(
    int Day,
    double EquivalentAgeDays,
    double EstimatedPsi,
    string Basis,
    string CalibrationVersion
);
