using Vulcan.Application.Interfaces;
using Vulcan.Domain.Enums;
using Vulcan.Prediction.Adjustments;
using Vulcan.Prediction.Strength;

namespace Vulcan.Application.Services;

public class StrengthEstimateService : IStrengthEstimateService
{
    public StrengthEstimateResult Estimate(
        DateTime castAt,
        CementType cementType,
        double expectedFinalBreakPsi,
        IReadOnlyList<StrengthSpecimen> specimens,
        StrengthSettings settings,
        IReadOnlyList<StrengthBreakMeasurement>? breakMeasurements = null
    ) => StrengthEstimator.Estimate(
        castAt,
        cementType,
        expectedFinalBreakPsi,
        specimens,
        settings,
        breakMeasurements
    );
}
