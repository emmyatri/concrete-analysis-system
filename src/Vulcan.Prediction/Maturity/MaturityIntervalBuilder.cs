
namespace Vulcan.Prediction.Maturity;

/// <summary>
/// Builds curing temperature intervals for maturity calculations.
/// </summary>
internal static class MaturityIntervalBuilder
{
    public static List<MaturityInterval> Build(
        DateTime castAt,
        IReadOnlyList<TemperatureReading> readings,
        DateTime targetTime)
    {
        var intervals = new List<MaturityInterval>();

        // Only use readings recorded at or after casting.
        var validReadings = readings
            .Where(r => r.TakenAt >= castAt)
            .OrderBy(r => r.TakenAt)
            .ToList();

        if (validReadings.Count == 0)
        {
            return intervals;
        }

        // The first recorded temperature applies from casting.
        var firstReading = validReadings[0];

        // The target may occur before the first recorded reading.
        if (targetTime <= firstReading.TakenAt)
        {
            if (targetTime > castAt)
            {
                intervals.Add(new MaturityInterval(
                    castAt,
                    targetTime,
                    firstReading.InternalTempF));
            }

            return intervals;
        }

        var applicableReadings = validReadings
            .Where(r => r.TakenAt <= targetTime)
            .ToList();

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

            double averageTempF =
                (previous.InternalTempF + current.InternalTempF) / 2.0;

            intervals.Add(new MaturityInterval(
                previous.TakenAt,
                current.TakenAt,
                averageTempF));
        }

        // Extend the calculation from the last reading to the target.
        var lastReading = applicableReadings[^1];

        if (lastReading.TakenAt < targetTime)
        {
            double averageTempF =
                applicableReadings.Average(r => r.InternalTempF);

            // Interpolate if a later reading is available.
            var nextReading = validReadings
                .FirstOrDefault(r => r.TakenAt > targetTime);

            if (nextReading is not null)
            {
                double totalHours =
                    (nextReading.TakenAt - lastReading.TakenAt).TotalHours;

                double elapsedHours =
                    (targetTime - lastReading.TakenAt).TotalHours;

                double fraction = elapsedHours / totalHours;

                double targetTempF = lastReading.InternalTempF +
                    (nextReading.InternalTempF - lastReading.InternalTempF)
                    * fraction;

                averageTempF =
                    (lastReading.InternalTempF + targetTempF) / 2.0;
            }

            intervals.Add(new MaturityInterval(
                lastReading.TakenAt,
                targetTime,
                averageTempF));
        }

        return intervals;
    }
}
