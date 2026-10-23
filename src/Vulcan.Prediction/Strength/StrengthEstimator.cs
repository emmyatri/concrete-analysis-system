using Vulcan.Domain.Enums;
using Vulcan.Prediction.Adjustments;
using Vulcan.Prediction.Maturity;

namespace Vulcan.Prediction.Strength;

public static class StrengthEstimator
{
    public static StrengthEstimateResult Estimate(
        DateTime castAt,
        CementType cementType,
        double expectedFinalBreakPsi,
        IReadOnlyList<StrengthSpecimen> specimens,
        StrengthSettings settings,
        IReadOnlyList<StrengthBreakMeasurement>? breakMeasurements = null)
    {
        ArgumentNullException.ThrowIfNull(specimens);
        ArgumentNullException.ThrowIfNull(settings);

        breakMeasurements ??= Array.Empty<StrengthBreakMeasurement>();

        // Match M2-12 timestamp conventions.
        // SQLite may return UTC timestamps with an Unspecified kind.
        if (castAt.Kind == DateTimeKind.Local)
        {
            throw new ArgumentException(
                "Cast timestamp must be UTC or Unspecified.",
                nameof(castAt));
        }

        castAt = DateTime.SpecifyKind(castAt, DateTimeKind.Utc);

        if (!double.IsFinite(expectedFinalBreakPsi) ||
            expectedFinalBreakPsi <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedFinalBreakPsi),
                "Expected final break PSI must be positive and finite.");
        }

        if (!double.IsFinite(settings.A) ||
            !double.IsFinite(settings.B) ||
            settings.A <= 0 ||
            settings.B <= 0)
        {
            throw new ArgumentException(
                "Calibration coefficients A and B must be positive and finite.",
                nameof(settings));
        }

        if (string.IsNullOrWhiteSpace(settings.CalibrationVersion))
        {
            throw new ArgumentException(
                "A calibration version is required.",
                nameof(settings));
        }

        // Cement-specific coefficients are supplied by the caller through
        // StrengthSettings. M2-20 is responsible for selecting the correct
        // calibration for the requested cement type.
        if (!Enum.IsDefined(cementType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(cementType),
                "Unsupported cement type.");
        }

        if (settings.CementType != cementType)
        {
            throw new ArgumentException(
                "Calibration cement type does not match the requested cement type.",
                nameof(settings));
        }

        // Validate and normalize all specimen readings before qualification.
        // Invalid data must not silently produce misleading estimates.
        var validatedSpecimens = new List<StrengthSpecimen>();

        foreach (var specimen in specimens)
        {
            if (specimen is null)
            {
                throw new ArgumentException(
                    "Specimen collection cannot contain null entries.",
                    nameof(specimens));
            }

            if (specimen.Readings is null)
            {
                throw new ArgumentException(
                    $"Specimen {specimen.SpecimenId} has a null reading collection.",
                    nameof(specimens));
            }

            var validatedReadings = new List<TemperatureReading>();

            foreach (var reading in specimen.Readings)
            {
                if (reading is null)
                {
                    throw new ArgumentException(
                        $"Specimen {specimen.SpecimenId} contains a null reading.",
                        nameof(specimens));
                }

                if (reading.TakenAt.Kind == DateTimeKind.Local)
                {
                    throw new ArgumentException(
                        $"Specimen {specimen.SpecimenId} contains a local timestamp.",
                        nameof(specimens));
                }

                if (!double.IsFinite(reading.InternalTempF))
                {
                    throw new ArgumentException(
                        $"Specimen {specimen.SpecimenId} contains a non-finite temperature.",
                        nameof(specimens));
                }

                // Treat Unspecified timestamps as UTC without changing
                // their clock values, consistent with M2-12.
                validatedReadings.Add(reading with
                {
                    TakenAt = DateTime.SpecifyKind(
                        reading.TakenAt,
                        DateTimeKind.Utc)
                });
            }

            // Duplicate timestamps would create ambiguous curing intervals.
            if (validatedReadings
                .GroupBy(r => r.TakenAt)
                .Any(group => group.Count() > 1))
            {
                throw new ArgumentException(
                    $"Specimen {specimen.SpecimenId} contains duplicate reading timestamps.",
                    nameof(specimens));
            }

            validatedSpecimens.Add(specimen with
            {
                Readings = validatedReadings
            });
        }

        // A specimen qualifies only when it has at least two
        // readings at or after casting and its latest reading
        // is at least 24 hours after casting.
        var qualifyingSpecimens = validatedSpecimens
            .Where(specimen =>
            {
                var validReadings = specimen.Readings
                    .Where(r => r.TakenAt >= castAt)
                    .ToList();

                return validReadings.Count >= 2 &&
                       validReadings.Max(r => r.TakenAt) >=
                       castAt.AddHours(24);
            })
            .ToList();

        if (qualifyingSpecimens.Count == 0)
        {
            return new StrengthEstimateResult
            {
                DailyCurve = Array.Empty<StrengthCurvePoint>(),
                Day3 = null,
                Day7 = null,
                Day28 = null,
                Basis = string.Empty,
                CalibrationVersion = settings.CalibrationVersion,
                StatusMessage = "Not enough curing data yet"
            };
        }

        // Calculate average equivalent age for each calendar day.
        var dailyCurve = new List<StrengthCurvePoint>();

        for (int day = 1; day <= 28; day++)
        {
            DateTime targetTime = castAt.AddDays(day);

            double totalEquivalentAge = 0;

            foreach (var specimen in qualifyingSpecimens)
            {
                MaturityResult maturity = MaturityCalculator.Calculate(
                    castAt,
                    specimen.Readings,
                    targetTime,
                    settings.Maturity);

                totalEquivalentAge += maturity.EquivalentAgeDays;
            }

            double averageEquivalentAge =
                totalEquivalentAge / qualifyingSpecimens.Count;

            // Apply the ACI strength equation once to the
            // averaged equivalent age, not per specimen.
            double estimatedPsi = averageEquivalentAge /
                                  (settings.A + settings.B * averageEquivalentAge)
                                  * expectedFinalBreakPsi;

            if (!double.IsFinite(averageEquivalentAge) ||
                !double.IsFinite(estimatedPsi) ||
                estimatedPsi < 0)
            {
                throw new ArithmeticException(
                    $"Strength calculation produced an invalid result for day {day}.");
            }

            dailyCurve.Add(new StrengthCurvePoint(
                day,
                averageEquivalentAge,
                estimatedPsi));
        }

        // Average recorded breaks for the current adjusted curve.
        // The aggregator validates measurements and rejects duplicates.
        double? measuredDay3Psi = BreakMeasurementAggregator.GetAveragePsi(
            breakMeasurements,
            day: 3);

        double? measuredDay7Psi = BreakMeasurementAggregator.GetAveragePsi(
            breakMeasurements,
            day: 7);

        // Reuse the equivalent ages already calculated by M2-13.
        double equivalentAgeDay3 = dailyCurve[2].EquivalentAgeDays;
        double equivalentAgeDay7 = dailyCurve[6].EquivalentAgeDays;

        // Preserve the original 3-day prediction for the ratio safeguard.
        double unadjustedDay3Psi = dailyCurve[2].EstimatedPsi;

        // Apply the same available break measurements across the
        // current curve, preserving the original predictions.
        var adjustedDailyCurve = new List<StrengthCurvePoint>();

        BreakAdjustmentResult? day3Adjustment = null;
        BreakAdjustmentResult? day7Adjustment = null;
        BreakAdjustmentResult? day28Adjustment = null;

        foreach (StrengthCurvePoint point in dailyCurve)
        {
            BreakAdjustmentResult adjustment = BreakAdjustmentCalculator.Calculate(
                equivalentAgeDays: point.EquivalentAgeDays,
                unadjustedPsi: point.EstimatedPsi,
                originalA: settings.A,
                b: settings.B,
                expectedFinalBreakPsi: expectedFinalBreakPsi,
                equivalentAgeDay3: equivalentAgeDay3,
                measuredDay3Psi: measuredDay3Psi,
                equivalentAgeDay7: equivalentAgeDay7,
                measuredDay7Psi: measuredDay7Psi,
                unadjustedDay3Psi: unadjustedDay3Psi);

            adjustment = adjustment with
            {
                Basis = adjustment.Stage switch
                {
                    BreakAdjustmentStage.ThreeAndSevenDay =>
                        $"Re-fit through the average 3- and 7-day breaks " +
                        $"(reference strength {adjustment.ReferenceStrengthPsi:N0} PSI).",

                    BreakAdjustmentStage.ThreeDay =>
                        "Re-fit through the average 3-day breaks " +
                        "using the original expected final strength.",

                    _ => adjustment.FallbackReason is not null
                        ? $"No break adjustment applied ({adjustment.FallbackReason}); " +
                          "original ACI 209R-92 prediction."
                        : "No break adjustment applied; original ACI 209R-92 prediction."
                }
            };

            adjustedDailyCurve.Add(new StrengthCurvePoint(
                point.Day,
                point.EquivalentAgeDays,
                adjustment.AdjustedPsi));

            switch (point.Day)
            {
                case 3:
                    day3Adjustment = adjustment;
                    break;
                case 7:
                    day7Adjustment = adjustment;
                    break;
                case 28:
                    day28Adjustment = adjustment;
                    break;
            }
        }

        bool usesExtrapolation = qualifyingSpecimens.Any(
            specimen => specimen.Readings.Max(r => r.TakenAt)
                        < castAt.AddDays(28));

        string specimenLabel = qualifyingSpecimens.Count == 1
            ? "specimen"
            : "specimens";

        double earliestLastReadingHours = qualifyingSpecimens.Min(
            specimen => (specimen.Readings.Max(r => r.TakenAt) - castAt)
                .TotalHours);

        double latestLastReadingHours = qualifyingSpecimens.Max(
            specimen => (specimen.Readings.Max(r => r.TakenAt) - castAt)
                .TotalHours);

        string readingCoverage = earliestLastReadingHours == latestLastReadingHours
            ? $"{latestLastReadingHours:0.#} hours"
            : $"{earliestLastReadingHours:0.#}–{latestLastReadingHours:0.#} hours";

        string basis =
            $"ACI 209R-92 ({cementType}) using " +
            $"{qualifyingSpecimens.Count} qualifying {specimenLabel}, " +
            $"with last curing readings at {readingCoverage} after casting " +
            $"and reference strength {expectedFinalBreakPsi:0} PSI. " +
            "Equivalent curing ages were averaged across qualifying specimens." +
            (usesExtrapolation
                ? " Beyond each specimen's last recorded temperature, " +
                  "the average of its available curing temperatures was assumed."
                : "");

        StrengthDayEstimate CreateDayEstimate(StrengthCurvePoint point)
        {
            return new StrengthDayEstimate(
                point.Day,
                point.EquivalentAgeDays,
                point.EstimatedPsi,
                basis,
                settings.CalibrationVersion);
        }

        // Day3, Day7, Day28, and DailyCurve contain the original,
        // unadjusted strength predictions.
        //
        // AdjustedDailyCurve and Day3Adjustment, Day7Adjustment,
        // and Day28Adjustment contain the break-adjusted predictions.
        //
        // Consumers displaying predicted strength should use the adjusted
        // values when an adjustment is available.

        return new StrengthEstimateResult
        {
            DailyCurve = dailyCurve,
            Day3 = CreateDayEstimate(dailyCurve[2]),
            Day7 = CreateDayEstimate(dailyCurve[6]),
            Day28 = CreateDayEstimate(dailyCurve[27]),
            Basis = basis,
            CalibrationVersion = settings.CalibrationVersion,
            StatusMessage = null,
            AdjustedDailyCurve = adjustedDailyCurve,
            Day3Adjustment = day3Adjustment,
            Day7Adjustment = day7Adjustment,
            Day28Adjustment = day28Adjustment,
        };
    }
}
