using Vulcan.Domain.Enums;
using Vulcan.Prediction.Adjustments;
using Vulcan.Prediction.Strength;

namespace Vulcan.Application.Interfaces;

public interface IStrengthEstimateService
{
    public StrengthEstimateResult Estimate(
        DateTime castAt,
        CementType cementType,
        double expectedFinalBreakPsi,
        IReadOnlyList<StrengthSpecimen> specimens,
        StrengthSettings settings,
        IReadOnlyList<StrengthBreakMeasurement>? breakMeasurements = null
    );
}
