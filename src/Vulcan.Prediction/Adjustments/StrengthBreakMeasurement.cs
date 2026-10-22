namespace Vulcan.Prediction.Adjustments;

public sealed record StrengthBreakMeasurement(
    string SpecimenId,
    int Day,
    double StrengthPsi,
    bool IsSynthetic
);
