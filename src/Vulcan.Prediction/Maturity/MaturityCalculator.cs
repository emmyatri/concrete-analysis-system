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

        return new MaturityResult(0, 0);
    }
}
