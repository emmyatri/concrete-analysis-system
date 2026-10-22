namespace Vulcan.Prediction.Adjustments;

public static class BreakAdjustmentCalculator
{
    public static double? CalculateThreeDayAdjustedA(
        double equivalentAgeDay3,
        double measuredDay3Psi,
        double unadjustedDay3Psi,
        double expectedFinalBreakPsi,
        double b)
    {
        if (!double.IsFinite(equivalentAgeDay3) ||
            !double.IsFinite(measuredDay3Psi) ||
            !double.IsFinite(unadjustedDay3Psi) ||
            !double.IsFinite(expectedFinalBreakPsi) ||
            !double.IsFinite(b) ||
            equivalentAgeDay3 <= 0 ||
            measuredDay3Psi <= 0 ||
            unadjustedDay3Psi <= 0 ||
            expectedFinalBreakPsi <= 0 ||
            b <= 0)
        {
            return null;
        }

        double ratio = measuredDay3Psi / unadjustedDay3Psi;

        if (ratio < 0.7 || ratio > 1.3)
        {
            return null;
        }

        double adjustedA = equivalentAgeDay3 *
                           (expectedFinalBreakPsi / measuredDay3Psi - b);

        if (!double.IsFinite(adjustedA) || adjustedA <= 0)
        {
            return null;
        }

        return adjustedA;
    }
    
    public static (double AdjustedA, double ReferenceStrengthPsi)?
        CalculateThreeAndSevenDayAdjustment(
            double equivalentAgeDay3,
            double equivalentAgeDay7,
            double measuredDay3Psi,
            double measuredDay7Psi,
            double b)
    {
        if (!double.IsFinite(equivalentAgeDay3) ||
            !double.IsFinite(equivalentAgeDay7) ||
            !double.IsFinite(measuredDay3Psi) ||
            !double.IsFinite(measuredDay7Psi) ||
            !double.IsFinite(b) ||
            equivalentAgeDay3 <= 0 ||
            equivalentAgeDay7 <= equivalentAgeDay3 ||
            measuredDay3Psi <= 0 ||
            measuredDay7Psi <= 0 ||
            b <= 0)
        {
            return null;
        }

        double denominator =
            1.0 / equivalentAgeDay3 -
            1.0 / equivalentAgeDay7;

        double k =
            (1.0 / measuredDay3Psi -
             1.0 / measuredDay7Psi) / denominator;

        double c = 1.0 / measuredDay3Psi -
                   k / equivalentAgeDay3;

        if (!double.IsFinite(k) ||
            !double.IsFinite(c) ||
            c <= 0)
        {
            return null;
        }

        double referenceStrengthPsi = b / c;
        double adjustedA = k * referenceStrengthPsi;

        if (!double.IsFinite(referenceStrengthPsi) ||
            !double.IsFinite(adjustedA) ||
            referenceStrengthPsi <= 0 ||
            adjustedA <= 0)
        {
            return null;
        }

        return (adjustedA, referenceStrengthPsi);
    }
    
    public static BreakAdjustmentResult Calculate(
        double equivalentAgeDays,
        double unadjustedPsi,
        double originalA,
        double b,
        double expectedFinalBreakPsi,
        double? equivalentAgeDay3,
        double? measuredDay3Psi,
        double? equivalentAgeDay7,
        double? measuredDay7Psi,
        double unadjustedDay3Psi)
    {
        double adjustedA = originalA;
        double referenceStrengthPsi = expectedFinalBreakPsi;
        BreakAdjustmentStage stage = BreakAdjustmentStage.None;
        string? fallbackReason = null;
    
        bool hasDay3 = equivalentAgeDay3.HasValue &&
                       measuredDay3Psi.HasValue;
    
        bool hasDay7 = equivalentAgeDay7.HasValue &&
                       measuredDay7Psi.HasValue;
    
        if (hasDay3 && hasDay7)
        {
            var twoBreakFit = CalculateThreeAndSevenDayAdjustment(
                equivalentAgeDay3!.Value,
                equivalentAgeDay7!.Value,
                measuredDay3Psi!.Value,
                measuredDay7Psi!.Value,
                b);
    
            if (twoBreakFit.HasValue)
            {
                adjustedA = twoBreakFit.Value.AdjustedA;
                referenceStrengthPsi =
                    twoBreakFit.Value.ReferenceStrengthPsi;
    
                stage = BreakAdjustmentStage.ThreeAndSevenDay;
            }
            else
            {
                fallbackReason = "3- and 7-day adjustment could not be fitted";
            }
        }
    
        if (stage == BreakAdjustmentStage.None && hasDay3)
        {
            double? threeDayA = CalculateThreeDayAdjustedA(
                equivalentAgeDay3!.Value,
                measuredDay3Psi!.Value,
                unadjustedDay3Psi,
                expectedFinalBreakPsi,
                b);
    
            if (threeDayA.HasValue)
            {
                adjustedA = threeDayA.Value;
                stage = BreakAdjustmentStage.ThreeDay;
            }
            else
            {
                double ratio = measuredDay3Psi!.Value / unadjustedDay3Psi;
    
                if (double.IsFinite(ratio) &&
                    (ratio < 0.7 || ratio > 1.3))
                {
                    fallbackReason = "3-day breaks are far from the estimate";
                }
                else
                {
                    fallbackReason ??= "3-day adjustment could not be fitted";
                }
            }
        }
    
        double adjustedPsi = stage == BreakAdjustmentStage.None
            ? unadjustedPsi
            : equivalentAgeDays /
              (adjustedA + b * equivalentAgeDays) *
              referenceStrengthPsi;
    
        return new BreakAdjustmentResult(
            stage,
            unadjustedPsi,
            adjustedPsi,
            adjustedA,
            referenceStrengthPsi,
            fallbackReason);
    }
}
