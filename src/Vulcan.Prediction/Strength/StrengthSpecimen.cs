using Vulcan.Prediction.Maturity;

namespace Vulcan.Prediction.Strength;

public sealed record StrengthSpecimen
{
    public required string SpecimenId { get; init; }

    public required IReadOnlyList<TemperatureReading> Readings { get; init; }
}
