using Vulcan.Prediction.Maturity;

namespace Vulcan.Prediction.Strength;

public sealed record StrengthSettings
{
    public required double A { get; init; }

    public required double B { get; init; }

    public required string CalibrationVersion { get; init; }

    public MaturitySettings Maturity { get; init; } = new();
}
