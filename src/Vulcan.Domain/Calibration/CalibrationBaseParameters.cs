using Vulcan.Domain.Enums;

namespace Vulcan.Domain.Calibration;

public sealed record CalibrationBaseParameters(
    decimal DatumTemperatureF,
    decimal ReferenceTemperatureF,
    Dictionary<CementType, CementTypeCalibration> CementTypeCalibrations);
