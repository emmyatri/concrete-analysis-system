using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Prediction.Maturity;

namespace Vulcan.Prediction.Strength;

public sealed record StrengthSettings
{
    public required CementType CementType { get; init; }

    public required double A { get; init; }

    public required double B { get; init; }

    public required string CalibrationVersion { get; init; }

    public MaturitySettings Maturity { get; init; } = new();

    public static StrengthSettings FromCalibration(CalibrationVersion cv, CementType selector)
    {
        var settings = cv.ParametersJson.Base.CementTypeCalibrations.GetValueOrDefault(selector)
            ?? throw new ArgumentException(
                "Stored calibration version does not have settings for the provided cement type.",
                nameof(cv));

        return new()
        {
            CementType = selector,
            A = (double)settings.TypeACoefficient,
            B = (double)settings.TypeBCoefficient,
            CalibrationVersion = cv.Version,
            Maturity = new MaturitySettings
            {
                DatumTempF = (double)cv.ParametersJson.Base.DatumTemperatureF,
                ReferenceTempF = (double)cv.ParametersJson.Base.ReferenceTemperatureF,
            }
        };
    }
}
