
namespace Vulcan.Prediction.Maturity;

public static class MaturityCalculator
{
    public static MaturityResult Calculate(
        DateTime castAt,
        IEnumerable<TemperatureReading> readings,
        DateTime targetTime,
        MaturitySettings settings)
    {
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(settings);

        // SQLite may return UTC timestamps with an Unspecified kind.
        // Reject Local timestamps, but interpret Unspecified as UTC.
        if (castAt.Kind == DateTimeKind.Local ||
            targetTime.Kind == DateTimeKind.Local)
        {
            throw new ArgumentException(
                "Cast and target timestamps must be UTC or Unspecified.");
        }

        castAt = DateTime.SpecifyKind(castAt, DateTimeKind.Utc);
        targetTime = DateTime.SpecifyKind(targetTime, DateTimeKind.Utc);

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

        // Validate all supplied readings before filtering.
        var validatedReadings = readings.ToList();

        for (int i = 0; i < validatedReadings.Count; i++)
        {
            var reading = validatedReadings[i];

            if (reading.TakenAt.Kind == DateTimeKind.Local)
            {
                throw new ArgumentException(
                    "Temperature reading timestamps must be UTC or Unspecified.");
            }

            if (!double.IsFinite(reading.InternalTempF))
            {
                throw new ArgumentException(
                    "Temperature readings must contain finite values.");
            }

            // Normalize SQLite timestamps without changing their clock values.
            validatedReadings[i] = reading with
            {
                TakenAt = DateTime.SpecifyKind(
                    reading.TakenAt,
                    DateTimeKind.Utc)
            };
        }

        // Reject duplicate timestamps to prevent ambiguous intervals.
        var duplicateTimestamp = validatedReadings
            .GroupBy(r => r.TakenAt)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateTimestamp is not null)
        {
            throw new ArgumentException(
                "Temperature readings cannot contain duplicate timestamps.",
                nameof(readings));
        }

        // A reading after the target is valid because the first
        // recorded temperature applies from casting.
        if (!validatedReadings.Any(r => r.TakenAt >= castAt))
        {
            throw new ArgumentException(
                "At least one temperature reading is required at or after casting.",
                nameof(readings));
        }

        // Build curing intervals using the approved interpolation behavior.
        var intervals = MaturityIntervalBuilder.Build(
            castAt,
            validatedReadings,
            targetTime);

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
