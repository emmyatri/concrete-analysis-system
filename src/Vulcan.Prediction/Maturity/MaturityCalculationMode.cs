namespace Vulcan.Prediction.Maturity;

/// <summary>
/// Determines how temperature readings are used when calculating
/// maturity at a requested target timestamp.
/// </summary>
public enum MaturityCalculationMode
{
    // Uses only readings recorded at or before the target timestamp.
    AsOfTarget,

    // Allows later readings to interpolate temperature at the target.
    Retrospective
}
