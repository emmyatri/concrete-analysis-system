namespace Vulcan.Prediction.Adjustments;

public static class BreakMeasurementAggregator
{
    public static double? GetAveragePsi(
        IReadOnlyList<StrengthBreakMeasurement> measurements,
        int day,
        int? predictionDay = null)
    {
        ArgumentNullException.ThrowIfNull(measurements);

        // Historical predictions may only use earlier breaks.
        if (predictionDay.HasValue && day >= predictionDay.Value)
        {
            return null;
        }

        // Validate the complete input collection before averaging.
        var specimenIds = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var measurement in measurements)
        {
            if (measurement is null)
            {
                throw new ArgumentException(
                    "Break measurements cannot contain null entries.",
                    nameof(measurements));
            }

            if (string.IsNullOrWhiteSpace(measurement.SpecimenId))
            {
                throw new ArgumentException(
                    "Every break measurement requires a specimen ID.",
                    nameof(measurements));
            }

            if (!double.IsFinite(measurement.StrengthPsi) ||
                measurement.StrengthPsi <= 0)
            {
                throw new ArgumentException(
                    $"Specimen {measurement.SpecimenId} has invalid break PSI.",
                    nameof(measurements));
            }

            if (!specimenIds.Add(measurement.SpecimenId))
            {
                throw new ArgumentException(
                    $"Duplicate break measurement for specimen {measurement.SpecimenId}.",
                    nameof(measurements));
            }

            if (measurement.Day is not (3 or 7 or 28))
            {
                throw new ArgumentException(
                    $"Specimen {measurement.SpecimenId} has an unsupported break age.",
                    nameof(measurements));
            }
        }

        var matching = measurements
            .Where(m => m.Day == day)
            .ToList();

        if (matching.Count == 0)
        {
            return null;
        }

        double averagePsi = matching.Average(m => m.StrengthPsi);

        if (!double.IsFinite(averagePsi))
        {
            throw new ArithmeticException(
                "Average break PSI is not finite.");
        }

        return averagePsi;
    }
}
