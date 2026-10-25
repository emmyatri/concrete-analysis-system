namespace Vulcan.Domain.Calibration;

public sealed record CalibrationJsonParameters(
    CalibrationBaseParameters Base,
    Dictionary<string, double> Factors);
