namespace Vulcan.Prediction.Maturity;

public sealed record TemperatureReading(
    DateTime TakenAt,
    double InternalTempF
);
