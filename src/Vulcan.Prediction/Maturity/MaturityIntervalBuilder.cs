namespace Vulcan.Prediction.Maturity;

/// <summary>
/// Builds curing temperature intervals for maturity calculations.
/// </summary>
internal static class MaturityIntervalBuilder
{
    public static List<MaturityInterval> Build(
        DateTime castAt,
        IReadOnlyList<TemperatureReading> readings,
        DateTime targetTime,
        MaturityCalculationMode mode)
    {

        var intervals = new List<MaturityInterval>();

        // Only use readings within the requested calculation period.
        var applicableReadings = readings
            .Where(r => r.TakenAt >= castAt && r.TakenAt <= targetTime)
            .OrderBy(r => r.TakenAt)
            .ToList();

        if (applicableReadings.Count == 0)
        {
            return intervals;
        }

        // Assume the first recorded temperature applies from casting.
        var firstReading = applicableReadings[0];

        if (firstReading.TakenAt > castAt)
        {
            intervals.Add(new MaturityInterval(
                castAt,
                firstReading.TakenAt,
                firstReading.InternalTempF));
        }

        // Build intervals between consecutive readings.
        for (int i = 1; i < applicableReadings.Count; i++)
        {
            var previous = applicableReadings[i - 1];
            var current = applicableReadings[i];

            if (current.TakenAt <= previous.TakenAt)
            {
                continue;
            }

            double averageTempF =
                (previous.InternalTempF + current.InternalTempF) / 2.0;

            intervals.Add(new MaturityInterval(
                previous.TakenAt,
                current.TakenAt,
                averageTempF));
        }

        // Determine how to handle the interval from the final reading
        // to the requested target time.
        var lastReading = applicableReadings[^1];

        if (lastReading.TakenAt < targetTime)
        {
            double averageTempF =
                applicableReadings.Average(r => r.InternalTempF);

            if (mode == MaturityCalculationMode.Retrospective)
            {
                // Find the first reading after the target time.
                var nextReading = readings
                    .Where(r => r.TakenAt > targetTime)
                    .OrderBy(r => r.TakenAt)
                    .FirstOrDefault();

                if (nextReading is not null)
                {
                    // Interpolate temperature at the target timestamp.
                    double totalHours =
                        (nextReading.TakenAt - lastReading.TakenAt).TotalHours;

                    double elapsedHours =
                        (targetTime - lastReading.TakenAt).TotalHours;

                    double fraction = elapsedHours / totalHours;

                    double targetTempF = lastReading.InternalTempF +
                                         (nextReading.InternalTempF - lastReading.InternalTempF)
                                         * fraction;

                    // Average the starting and interpolated temperatures.
                    averageTempF =
                        (lastReading.InternalTempF + targetTempF) / 2.0;
                }
            }

            intervals.Add(new MaturityInterval(
                lastReading.TakenAt,
                targetTime,
                averageTempF));
        }

        return intervals;
    }
}
