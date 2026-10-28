using Vulcan.Application.Contracts;
using Vulcan.Application.Interfaces;
using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Interfaces;
using Vulcan.Domain.Services;

namespace Vulcan.Application.Services.EarlyWarning;

public class EarlyWarningService(
    IPredictionRepository prediction,
    IRecordWarningRepository recordWarning,
    ILimitProvider limitProvider,
    WaterCementRatioCalculator wcrCalculator)
{
    public async Task<EarlyWarningEvaluation> Evaluate(Batch batch)
    {
        var predictions = await prediction.GetByBatchIdAsync(batch.Id);
        var batchRecordWarnings = await recordWarning
            .GetForBatchesAsync([batch]);
        var existingBatchWarnings = batchRecordWarnings
            .Select(EarlyWarning.FromRecordWarning);

        // var warnings = new List<EarlyWarning>();

        // foreach (var p in predictions)
        // {
        //     warnings.AddRange(EvaluatePrediction(p));
        // }

        var (
            shortfallPct,
            shortfallPsi,
            projected28DayPsi,
            warnings
        ) = EvaluateBatch(batch, predictions);

        return new EarlyWarningEvaluation
        (
            AtRisk: warnings.Count > 0,
            ShortfallPsi: shortfallPsi,
            ShortfallPct: shortfallPct,
            Projected28DayPsi: projected28DayPsi,
            EarlyWarnings: [.. warnings.Concat(existingBatchWarnings).Select(w => w.ToDTO())]
        );
    }

    /// <summary>
    /// Returns (shortfall percent?, shortfall PSI?, projected 28 day psi, list of early warnings)
    /// </summary>
    /// <param name="batch"></param>
    /// <param name="predictions"></param>
    /// <returns></returns>
    private (double?, int?, double?, List<EarlyWarning>) EvaluateBatch(
        Batch batch,
        IEnumerable<PredictionEntity> predictions)
    {
        var warnings = new List<EarlyWarning>();
        double? shortfallPct = null;
        int? shortfallPsi = null;
        double? projected28DayPsi = null;

        foreach (var p in predictions)
        {
            if (p.HorizonDays == 28 && p.EstimatedPsi is { } current28DayEstimate)
            {
                // check shortfall percent
                var batchShortfallPct = CalculateShortfallPct(
                    batch.ExpectedFinalBreakPsi,
                    current28DayEstimate);

                var shortfallWarning = Check(
                    LimitKeys.PredictionShortfallPct,
                    batchShortfallPct,
                    nameof(current28DayEstimate));

                if (shortfallWarning is not null)
                {
                    warnings.Add(shortfallWarning);
                }

                // set return results
                shortfallPct = batchShortfallPct;
                shortfallPsi = (int)(batch.ExpectedFinalBreakPsi - current28DayEstimate);
                projected28DayPsi = (double?)p.EstimatedPsi;

                // check minimum psi
                var minimumPsiWarning = Check(
                    LimitKeys.PredictionMinimumPsi,
                    (double)current28DayEstimate,
                    nameof(current28DayEstimate));

                if (minimumPsiWarning is not null)
                {
                    warnings.Add(minimumPsiWarning);
                }
            }

            // rest of prediction checks
        }

        // check water-cement ratio
        if (batch.Composition is { } composition)
        {
            var waterCementRatio = wcrCalculator.Calculate(composition);
            // don't check wcm below 0.33; ADR-005, section 4.5
            if (waterCementRatio is { } wcm && wcm >= 0.33m)
            {
                var aciTablePsi = CalculateAciTablePsi((double)wcm);
                if (aciTablePsi == 0)
                {
                    return (shortfallPct, shortfallPsi, projected28DayPsi, warnings);
                }
                var expectedFinalVsWcmWarning = Check(
                    LimitKeys.ExpectedFinalVsWcmPct,
                    CalculateExpectedFinalVsWcmPct(
                        batch.ExpectedFinalBreakPsi,
                        aciTablePsi),
                    nameof(Batch.ExpectedFinalBreakPsi));

                if (expectedFinalVsWcmWarning is not null)
                {
                    warnings.Add(expectedFinalVsWcmWarning);
                }
            }
        }

        return (shortfallPct, shortfallPsi, projected28DayPsi, warnings);
    }

    // private List<EarlyWarning> EvaluatePrediction(PredictionEntity prediction)
    // {
    //     // usings lists to accommodate (likely?) future warnings
    //     var warnings = new List<EarlyWarning>();

    //     return warnings;
    // }

    // not using LimitEvalutor because an EarlyWarning must include what end of the range was
    // violated
    private EarlyWarning? Check(string key, double value, string field)
    {
        var limits = limitProvider.Get(key);

        EarlyWarning? CheckRange(ValidationLimit vl)
        {
            if (vl.Min is { } min && value < (double)min)
            {
                return EarlyWarning.FromValidationLimit(vl, field, value, (double)min);
            }

            if (vl.Max is { } max && value > (double)max)
            {
                return EarlyWarning.FromValidationLimit(vl, field, value, (double)max);
            }
            return null;
        }

        // additional null check to fall-through
        if (limits.Reject is { } reject && CheckRange(reject) is { } notNull)
        {
            return notNull;
        }

        if (limits.Warn is { } warn)
        {
            return CheckRange(warn);
        }

        return null;
    }

    // interpolate the water-cement ratio within the ACI table. Values beyond 0.82 are clamped to
    // 2000 according to ADR-005, section 4.5.
    private static double CalculateAciTablePsi(double wcm)
    {
        (double Wcm, double Psi)[] table =
        [
            (0.33, 7000),
            (0.41, 6000),
            (0.48, 5000),
            (0.57, 4000),
            (0.68, 3000),
            (0.82, 2000)
        ];

        if (wcm <= table[0].Wcm)
        {
            return table[0].Psi;
        }

        if (wcm >= table[^1].Wcm)
        {
            return table[^1].Psi;
        }

        for (var i = 0; i < table.Length - 1; i++)
        {
            var (lowerWcm, lowerPsi) = table[i];
            var (upperWcm, upperPsi) = table[i + 1];

            if (wcm >= lowerWcm && wcm <= upperWcm)
            {
                var proportion = (wcm - lowerWcm) / (upperWcm - lowerWcm);
                return lowerPsi + proportion * (upperPsi - lowerPsi);
            }
        }

        throw new InvalidOperationException("Unable to interpolate ACI table PSI.");
    }

    // based on ACI 211.1-97, Table 6.3.4(a)
    private static double CalculateShortfallPct(
        int expectedFinalBreakPsi,
        decimal current28DayEstimatePsi)
        => (double)((expectedFinalBreakPsi - current28DayEstimatePsi)
                / expectedFinalBreakPsi * 100);

    // based on ACI 211.1-97, Table 6.3.4(a)
    private static double CalculateExpectedFinalVsWcmPct(
        int expectedFinalBreakPsi,
        double aciTablePsi)
        => (expectedFinalBreakPsi - aciTablePsi)
            / aciTablePsi * 100;
}
