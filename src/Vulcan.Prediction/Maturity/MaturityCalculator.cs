namespace Vulcan.Prediction.Maturity;

public static class MaturityCalculator
{
    public static MaturityResult Calculate(
        DateTime castAt,
        IEnumerable<TemperatureReading> readings,
        DateTime targetTime,
        MaturitySettings settings,
        MaturityCalculationMode mode = MaturityCalculationMode.AsOfTarget)
    {
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(settings);

        if (castAt.Kind != DateTimeKind.Utc ||
            targetTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Cast and target timestamps must be UTC.");
        }

        if (targetTime < castAt)
        {
            throw new ArgumentException(
                "Target time cannot precede casting.");
        }

        if (!double.IsFinite(settings.DatumTempF) ||
            !double.IsFinite(settings.ReferenceTempF) ||
            settings.ReferenceTempF <= settings.DatumTempF)
        {
            throw new ArgumentException(
                "Reference temperature must be greater than datum temperature.");
        }

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        // Validate all supplied readings before filtering.
        var validatedReadings = readings.ToList();

        foreach (var reading in validatedReadings)
        {
            if (reading.TakenAt.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Temperature reading timestamps must be UTC.");
            }

            if (!double.IsFinite(reading.InternalTempF))
            {
                throw new ArgumentException(
                    "Temperature readings must contain finite values.");
            }
        }

        // Reject duplicate timestamps to prevent ambiguous temperature intervals.
        var duplicateTimestamp = validatedReadings
            .GroupBy(r => r.TakenAt)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateTimestamp is not null)
        {
            throw new ArgumentException(
                "Temperature readings cannot contain duplicate timestamps.",
                nameof(readings));
        }

        // Require at least one temperature reading within the calculation period.
        // Missing data must not be interpreted as zero maturity.
        if (!validatedReadings.Any(r =>
                r.TakenAt >= castAt &&
                r.TakenAt <= targetTime))
        {
            throw new ArgumentException(
                "At least one temperature reading is required between casting and the target time.",
                nameof(readings));
        }

        // Build curing intervals using the selected calculation mode.
        var intervals = MaturityIntervalBuilder.Build(
            castAt,
            validatedReadings,
            targetTime,
            mode);

        double maturityFHours = 0;

        // Accumulate Nurse–Saul maturity across all curing intervals.
        foreach (var interval in intervals)
        {
            double elapsedHours =
                (interval.End - interval.Start).TotalHours;

            if (elapsedHours <= 0)
            {
                continue;
            }

            double temperatureDifference = Math.Max(
                0,
                interval.AverageTempF - settings.DatumTempF);

            maturityFHours += temperatureDifference * elapsedHours;
        }

        // Convert accumulated maturity into equivalent age in days.
        double equivalentAgeDays = maturityFHours /
            ((settings.ReferenceTempF - settings.DatumTempF) * 24);

        return new MaturityResult(maturityFHours, equivalentAgeDays);
    }
}
