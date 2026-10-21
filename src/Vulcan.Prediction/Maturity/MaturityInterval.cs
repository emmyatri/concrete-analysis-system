namespace Vulcan.Prediction.Maturity;

/// <summary>
/// Represents a curing interval with an average internal temperature.
/// </summary>
internal sealed record MaturityInterval(
    DateTime Start,
    DateTime End,
    double AverageTempF
);
