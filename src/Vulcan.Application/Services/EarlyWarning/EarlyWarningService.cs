using System.Globalization;
using Vulcan.Application.Contracts;
using Vulcan.Application.Interfaces;
using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Interfaces;
using Vulcan.Domain.Services;

namespace Vulcan.Application.Services.EarlyWarning;

public class EarlyWarningService(
    IPredictionRepository prediction,
    IRecordWarningRepository recordWarning,
    ILimitProvider limitProvider,
    WaterCementRatioCalculator wcrCalculator) : IEarlyWarningEvaluator
{
    public Task<EarlyWarningEvaluation> EvaluateAsync(Batch batch, CancellationToken cancellationToken)
        => Evaluate(batch);

    public async Task<EarlyWarningEvaluation> Evaluate(Batch batch)
    {
        var predictions = await prediction.GetByBatchIdAsync(batch.Id);
        var batchRecordWarnings = await recordWarning.GetForBatchesAsync([batch]);

        // Saved break warnings (28-day breaks below 3,000 PSI) aren't early warnings (client-decisions §13);
        // the 3- and 7-day bands are worked out in EvaluateEarlyBreaks instead. One entry per factor: a cold
        // batch has a saved ambient warning on every reading, but it is one reason, listed once.
        var savedWarnings = batchRecordWarnings
            .Where(rw => rw.EntityType != nameof(BreakResult))
            .GroupBy(rw => rw.LimitKey)
            .Select(group => group.OrderBy(rw => rw.Id).First())
            .Select(EarlyWarning.FromRecordWarning);

        var (shortfallPct, shortfallPsi, projected28DayPsi, warnings) = EvaluateBatch(batch, predictions);
        var breakWarnings = EvaluateEarlyBreaks(batch);

        var predictionWarnings = warnings
            .Where(w => w.LimitKey.StartsWith("Prediction.", StringComparison.Ordinal))
            .ToList();
        var batchWarnings = warnings.Except(predictionWarnings);

        return new EarlyWarningEvaluation(
            AtRisk: predictionWarnings.Count > 0 || breakWarnings.Count > 0,
            Projected28DayPsi: projected28DayPsi,
            ShortfallPsi: shortfallPsi,
            ShortfallPct: shortfallPct,
            EarlyWarnings: [.. predictionWarnings
                .Concat(breakWarnings)
                .Concat(savedWarnings)
                .Concat(batchWarnings)
                .Select(w => w.ToDTO())]);
    }

    private static readonly (TestAgeDays Age, string Key, string Field, string Outcome)[] _earlyBreakBands =
    [
        (TestAgeDays.Day3, LimitKeys.BreakDay3WarnPct, "day3AverageBreakPct", "Check the 7-day breaks."),
        (TestAgeDays.Day7, LimitKeys.BreakDay7FailPct, "day7AverageBreakPct", "The batch will probably fail.")
    ];

    /// <summary>
    /// Client-decisions §14.1: a low average 3-day break is an early warning, and a low average 7-day
    /// break means the batch will probably fail. The bands are a percent of the 3,000 PSI break minimum
    /// (the Break.StrengthPsi Warn Min), not of the batch's expected final. Worked out from the breaks on
    /// every evaluation rather than saved with each break, because the average moves as cylinders are entered.
    /// </summary>
    private List<EarlyWarning> EvaluateEarlyBreaks(Batch batch)
    {
        var warnings = new List<EarlyWarning>();
        if (limitProvider.Get(LimitKeys.StrengthPsi).Warn?.Min is not { } minimum || minimum <= 0)
        {
            return warnings;
        }

        var minimumPsi = (double)minimum;
        foreach (var (age, key, field, outcome) in _earlyBreakBands)
        {
            var breaks = batch.Specimens
                .Where(s => s.TestAgeDays == age && s.BreakResult is not null)
                .Select(s => s.BreakResult!.StrengthPsi)
                .ToList();
            if (breaks.Count == 0)
            {
                continue;
            }

            var averagePsi = Math.Round(breaks.Average(), 0, MidpointRounding.AwayFromZero);
            var pctOfMinimum = Math.Round(averagePsi / minimumPsi * 100, 1, MidpointRounding.AwayFromZero);

            if (Check(key, pctOfMinimum, field) is { } warning)
            {
                var bandPsi = Math.Round(warning.Limit * minimumPsi / 100, 0, MidpointRounding.AwayFromZero);
                warning.Warning =
                    $"{(int)age}-day breaks averaged {Psi(averagePsi)}, below {Psi(bandPsi)} ({warning.Limit.ToString("0.#", CultureInfo.InvariantCulture)}% of {Psi(minimumPsi)}). {outcome}";
                warnings.Add(warning);
            }
        }

        return warnings;
    }

    /// <summary>
    /// Returns (shortfall percent?, shortfall PSI?, projected 28-day PSI?, early warnings).
    /// Message wording matches mock-strength.ts, which the M2 acceptance script is verified against.
    /// </summary>
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
                // Whole PSI, rounded the same way as the strength endpoint (SCRUM-169).
                var projected = Math.Round(current28DayEstimate, 0, MidpointRounding.AwayFromZero);
                var batchShortfallPct = Math.Round(
                    CalculateShortfallPct(batch.ExpectedFinalBreakPsi, projected), 2, MidpointRounding.AwayFromZero);

                var minimumPsiWarning = Check(LimitKeys.PredictionMinimumPsi, (double)projected, "projected28DayPsi");
                if (minimumPsiWarning is not null)
                {
                    minimumPsiWarning.Warning =
                        $"Projected 28-day strength of {Psi((double)projected)} is below the {Psi(minimumPsiWarning.Limit)} minimum";
                    warnings.Add(minimumPsiWarning);
                }

                var shortfallWarning = Check(LimitKeys.PredictionShortfallPct, batchShortfallPct, "shortfallPct");
                if (shortfallWarning is not null)
                {
                    shortfallWarning.Warning =
                        $"Projected 28-day strength of {Psi((double)projected)} is {batchShortfallPct.ToString("0.##", CultureInfo.InvariantCulture)}% below the expected {Psi(batch.ExpectedFinalBreakPsi)}";
                    warnings.Add(shortfallWarning);
                }

                shortfallPct = batchShortfallPct;
                shortfallPsi = batch.ExpectedFinalBreakPsi - (int)projected;
                projected28DayPsi = (double)projected;
            }
        }

        // Expected final vs. w/cm. Below 0.33 the check is skipped (ADR-005, section 4.5).
        // Round to 3 places like the displayed w/c, so the table lookup and the message match the screen.
        if (batch.Composition is { } composition
            && wcrCalculator.Calculate(composition) is { } rawWcm
            && Math.Round(rawWcm, 3, MidpointRounding.AwayFromZero) is var wcm
            && wcm >= 0.33m)
        {
            var aciTablePsi = CalculateAciTablePsi((double)wcm);
            var vsWcmPct = Math.Round(
                CalculateExpectedFinalVsWcmPct(batch.ExpectedFinalBreakPsi, aciTablePsi), 1, MidpointRounding.AwayFromZero);

            var expectedFinalVsWcmWarning = Check(LimitKeys.ExpectedFinalVsWcmPct, vsWcmPct, "expectedFinalVsWcmPct");
            if (expectedFinalVsWcmWarning is not null)
            {
                expectedFinalVsWcmWarning.Warning =
                    $"Expected final of {Psi(batch.ExpectedFinalBreakPsi)} is {vsWcmPct.ToString("0.#", CultureInfo.InvariantCulture)}% above the {Psi(Math.Round(aciTablePsi))} typical for a w/cm of {wcm.ToString("0.###", CultureInfo.InvariantCulture)}";
                warnings.Add(expectedFinalVsWcmWarning);
            }
        }

        return (shortfallPct, shortfallPsi, projected28DayPsi, warnings);
    }

    // Not using LimitEvaluator because an EarlyWarning must include which end of the range was violated.
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

        if (limits.Reject is { } reject && CheckRange(reject) is { } rejected)
        {
            return rejected;
        }

        return limits.Warn is { } warn ? CheckRange(warn) : null;
    }

    // Linear interpolation in ACI 211.1-97, Table 6.3.4(a). Beyond 0.82 the 2,000 PSI value is used (ADR-005, section 4.5).
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

    private static double CalculateShortfallPct(int expectedFinalBreakPsi, decimal projected28DayPsi)
        => (double)((expectedFinalBreakPsi - projected28DayPsi) / expectedFinalBreakPsi * 100);

    private static double CalculateExpectedFinalVsWcmPct(int expectedFinalBreakPsi, double aciTablePsi)
        => (expectedFinalBreakPsi - aciTablePsi) / aciTablePsi * 100;

    private static string Psi(double value) => $"{value.ToString("N0", CultureInfo.InvariantCulture)} PSI";
}
