
using Vulcan.Prediction.Maturity;

namespace Vulcan.Prediction.Tests.Maturity;

public class MaturityCalculatorTests
{
    // ============================================================
    // Core Maturity Calculations
    // ============================================================

    [Fact]
    public void Calculate_ConstantReferenceTemperature_ReturnsThreeEquivalentDays()
    {
        // Arrange: 73.4°F for 72 hours equals 3 equivalent days.
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var targetTime = castAt.AddHours(72);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 73.4),
            new(castAt.AddHours(24), 73.4),
            new(castAt.AddHours(48), 73.4),
            new(targetTime, 73.4)
        };

        var settings = new MaturitySettings();

        // Act
        var result = MaturityCalculator.Calculate(
            castAt,
            readings,
            targetTime,
            settings);

        // Assert: (73.4 - 32) * 72 = 2980.8 °F-hours.
        Assert.Equal(2980.8, result.MaturityFHours, precision: 6);
        Assert.Equal(3.0, result.EquivalentAgeDays, precision: 6);
    }

    [Fact]
    public void Calculate_PublishedNdotExample_ReturnsExpectedMaturity()
    {
        // Arrange: Nebraska DOT Nurse-Saul example.
        // Temperature increases from 20°C to 50°C over 3 hours.
        // Datum temperature: -10°C.
        // Published maturity: 135°C-hours = 243°F-hours.
        //
        // Fahrenheit conversions:
        // 20°C = 68°F, 50°C = 122°F, -10°C = 14°F.

        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var targetTime = castAt.AddHours(3);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 68.0),
            new(targetTime, 122.0)
        };

        var settings = new MaturitySettings
        {
            DatumTempF = 14.0,
            ReferenceTempF = 73.4
        };

        var result = MaturityCalculator.Calculate(
            castAt,
            readings,
            targetTime,
            settings);

        // Average temperature: (68 + 122) / 2 = 95°F.
        // Maturity: (95 - 14) * 3 = 243°F-hours.
        Assert.Equal(243.0, result.MaturityFHours, precision: 6);

        Assert.Equal(
            243.0 / ((73.4 - 14.0) * 24.0),
            result.EquivalentAgeDays,
            precision: 6);
    }

    [Fact]
    public void Calculate_BelowDatumTemperature_DoesNotAccumulateMaturity()
    {
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var targetTime = castAt.AddHours(48);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 20.0),
            new(castAt.AddHours(24), 25.0),
            new(targetTime, 30.0)
        };

        var result = MaturityCalculator.Calculate(
            castAt,
            readings,
            targetTime,
            new MaturitySettings());

        Assert.Equal(0.0, result.MaturityFHours, precision: 6);
        Assert.Equal(0.0, result.EquivalentAgeDays, precision: 6);
    }

    [Fact]
    public void Calculate_VmSyn006_D28_1_ReturnsExpectedDay3Maturity()
    {
        // Actual VM-SYN-006 / D28-1 synthetic fixture readings.
        var castAt = new DateTime(
            2026, 6, 21, 12, 0, 0, DateTimeKind.Utc);

        var targetTime = castAt.AddDays(3);

        var temperatures = new[]
        {
            78.3, 81.1, 82.9, 80.7, 78.4, 77.9, 76.1
        };

        var readings = temperatures
            .Select((temp, index) => new TemperatureReading(
                castAt.AddHours(index * 12),
                temp))
            .ToList();

        var result = MaturityCalculator.Calculate(
            castAt,
            readings,
            targetTime,
            new MaturitySettings());

        Assert.Equal(3434.4, result.MaturityFHours, precision: 6);
        Assert.Equal(
            3434.4 / 993.6,
            result.EquivalentAgeDays,
            precision: 6);
    }

    // ============================================================
    // Historical Target Calculations
    // ============================================================

    [Fact]
    public void Calculate_FutureReading_InterpolatesAtTarget()
    {
        // Target is between readings at hours 48 and 96.
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var targetTime = castAt.AddHours(72);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 70.0),
            new(castAt.AddHours(24), 80.0),
            new(castAt.AddHours(48), 60.0),
            new(castAt.AddHours(96), 120.0)
        };

        var result = MaturityCalculator.Calculate(
            castAt,
            readings,
            targetTime,
            new MaturitySettings());

        // Hours 0-24:  (75 - 32) * 24 = 1032
        // Hours 24-48: (70 - 32) * 24 = 912
        // Interpolated temperature at hour 72: 90°F
        // Hours 48-72: (75 - 32) * 24 = 1032
        // Total: 2976°F-hours.
        Assert.Equal(2976.0, result.MaturityFHours, precision: 6);
        Assert.Equal(
            2976.0 / 993.6,
            result.EquivalentAgeDays,
            precision: 6);
    }

    [Fact]
    public void Calculate_Day7Target_IgnoresReadingsAfterExactTarget()
    {
        // A reading exactly at the target closes the calculation.
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var targetTime = castAt.AddDays(7);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 73.4),
            new(castAt.AddDays(1), 73.4),
            new(castAt.AddDays(3), 73.4),
            new(castAt.AddDays(5), 73.4),
            new(castAt.AddDays(7), 73.4),
            new(castAt.AddDays(8), 100.0),
            new(castAt.AddDays(10), 120.0)
        };

        var result = MaturityCalculator.Calculate(
            castAt,
            readings,
            targetTime,
            new MaturitySettings());

        Assert.Equal(6955.2, result.MaturityFHours, precision: 6);
        Assert.Equal(7.0, result.EquivalentAgeDays, precision: 6);
    }

    // ============================================================
    // Interpolation and Fallback
    // ============================================================

    [Fact]
    public void Calculate_InterpolatesAtTargetTime()
    {
        // Target is halfway between hours 24 and 48.
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var targetTime = castAt.AddHours(36);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 60.0),
            new(castAt.AddHours(24), 80.0),
            new(castAt.AddHours(48), 100.0)
        };

        var result = MaturityCalculator.Calculate(
            castAt,
            readings,
            targetTime,
            new MaturitySettings());

        // Hours 0-24: (70 - 32) * 24 = 912
        // Interpolated temperature at hour 36: 90°F
        // Hours 24-36: (85 - 32) * 12 = 636
        // Total: 1548°F-hours.
        Assert.Equal(1548.0, result.MaturityFHours, precision: 6);
        Assert.Equal(
            1548.0 / 993.6,
            result.EquivalentAgeDays,
            precision: 6);
    }

    [Fact]
    public void Calculate_NoFutureReading_UsesAverageTemperatureFallback()
    {
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var targetTime = castAt.AddHours(36);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 60.0),
            new(castAt.AddHours(24), 80.0)
        };

        var result = MaturityCalculator.Calculate(
            castAt,
            readings,
            targetTime,
            new MaturitySettings());

        // Hours 0-24: (70 - 32) * 24 = 912
        // Hours 24-36: (70 - 32) * 12 = 456
        Assert.Equal(1368.0, result.MaturityFHours, precision: 6);
        Assert.Equal(
            1368.0 / 993.6,
            result.EquivalentAgeDays,
            precision: 6);
    }

    // ============================================================
    // Reading Order and Time Boundaries
    // ============================================================

    [Fact]
    public void Calculate_FirstReadingAfterCasting_UsesFirstTemperatureForInitialInterval()
    {
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var targetTime = castAt.AddHours(24);

        var readings = new List<TemperatureReading>
        {
            new(castAt.AddHours(-6), 100.0),
            new(castAt.AddHours(12), 70.0),
            new(targetTime, 80.0)
        };

        var result = MaturityCalculator.Calculate(
            castAt,
            readings,
            targetTime,
            new MaturitySettings());

        // Hours 0-12: (70 - 32) * 12 = 456
        // Hours 12-24: (75 - 32) * 12 = 516
        Assert.Equal(972.0, result.MaturityFHours, precision: 6);
        Assert.Equal(
            972.0 / 993.6,
            result.EquivalentAgeDays,
            precision: 6);
    }

    [Fact]
    public void Calculate_TargetBeforeFirstReading_UsesFirstTemperature()
    {
        // First reading at hour 26, target at hour 24.
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var targetTime = castAt.AddHours(24);

        var readings = new List<TemperatureReading>
        {
            new(castAt.AddHours(26), 75.0)
        };

        var result = MaturityCalculator.Calculate(
            castAt,
            readings,
            targetTime,
            new MaturitySettings());

        // (75 - 32) * 24 = 1032°F-hours.
        Assert.Equal(1032.0, result.MaturityFHours, precision: 6);
        Assert.Equal(
            1032.0 / 993.6,
            result.EquivalentAgeDays,
            precision: 6);
    }

    [Fact]
    public void Calculate_OutOfOrderReadings_ReturnsSameMaturity()
    {
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var targetTime = castAt.AddHours(48);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 70.0),
            new(castAt.AddHours(24), 80.0),
            new(targetTime, 60.0)
        };

        var settings = new MaturitySettings();

        var chronologicalResult = MaturityCalculator.Calculate(
            castAt,
            readings,
            targetTime,
            settings);

        var reversedResult = MaturityCalculator.Calculate(
            castAt,
            readings.AsEnumerable().Reverse(),
            targetTime,
            settings);

        Assert.Equal(
            chronologicalResult.MaturityFHours,
            reversedResult.MaturityFHours,
            precision: 6);

        Assert.Equal(
            chronologicalResult.EquivalentAgeDays,
            reversedResult.EquivalentAgeDays,
            precision: 6);

        Assert.Equal(1944.0, reversedResult.MaturityFHours, precision: 6);
    }

    [Fact]
    public void Calculate_TargetEqualsCastAt_ReturnsZeroMaturity()
    {
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 75.0)
        };

        var result = MaturityCalculator.Calculate(
            castAt,
            readings,
            castAt,
            new MaturitySettings());

        Assert.Equal(0.0, result.MaturityFHours, precision: 6);
        Assert.Equal(0.0, result.EquivalentAgeDays, precision: 6);
    }

    // ============================================================
    // Input Validation
    // ============================================================

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Calculate_InvalidTemperature_ThrowsArgumentException(
        double invalidTemperature)
    {
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 73.4),
            new(castAt.AddHours(24), invalidTemperature)
        };

        Assert.Throws<ArgumentException>(() =>
            MaturityCalculator.Calculate(
                castAt,
                readings,
                castAt.AddHours(48),
                new MaturitySettings()));
    }

    [Fact]
    public void Calculate_LocalReading_ThrowsArgumentException()
    {
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 73.4),
            new(
                new DateTime(
                    2026, 10, 21, 8, 0, 0, DateTimeKind.Local),
                75.0)
        };

        Assert.Throws<ArgumentException>(() =>
            MaturityCalculator.Calculate(
                castAt,
                readings,
                castAt.AddHours(48),
                new MaturitySettings()));
    }

    [Fact]
    public void Calculate_UnspecifiedTimestamps_AreTreatedAsUtc()
    {
        // SQLite may return UTC values with DateTimeKind.Unspecified.
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Unspecified);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 73.4),
            new(castAt.AddHours(24), 73.4)
        };

        var result = MaturityCalculator.Calculate(
            castAt,
            readings,
            castAt.AddHours(24),
            new MaturitySettings());

        Assert.Equal(993.6, result.MaturityFHours, precision: 6);
        Assert.Equal(1.0, result.EquivalentAgeDays, precision: 6);
    }

    [Fact]
    public void Calculate_LocalCastTimestamp_ThrowsArgumentException()
    {
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Local);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 73.4)
        };

        Assert.Throws<ArgumentException>(() =>
            MaturityCalculator.Calculate(
                castAt,
                readings,
                castAt.AddHours(24),
                new MaturitySettings()));
    }

    [Fact]
    public void Calculate_TargetBeforeCasting_ThrowsArgumentException()
    {
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 73.4)
        };

        Assert.Throws<ArgumentException>(() =>
            MaturityCalculator.Calculate(
                castAt,
                readings,
                castAt.AddHours(-1),
                new MaturitySettings()));
    }

    [Theory]
    [InlineData(32.0, 32.0)]
    [InlineData(80.0, 73.4)]
    [InlineData(double.NaN, 73.4)]
    [InlineData(32.0, double.PositiveInfinity)]
    public void Calculate_InvalidSettings_ThrowsArgumentException(
        double datumTempF,
        double referenceTempF)
    {
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 73.4),
            new(castAt.AddHours(24), 75.0)
        };

        var settings = new MaturitySettings
        {
            DatumTempF = datumTempF,
            ReferenceTempF = referenceTempF
        };

        Assert.Throws<ArgumentException>(() =>
            MaturityCalculator.Calculate(
                castAt,
                readings,
                castAt.AddHours(48),
                settings));
    }

    [Fact]
    public void Calculate_OnlyPreCastReadings_ThrowsArgumentException()
    {
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var readings = new List<TemperatureReading>
        {
            new(castAt.AddHours(-12), 70.0),
            new(castAt.AddHours(-1), 80.0)
        };

        Assert.Throws<ArgumentException>(() =>
            MaturityCalculator.Calculate(
                castAt,
                readings,
                castAt.AddHours(72),
                new MaturitySettings()));
    }

    [Fact]
    public void Calculate_NoReadings_ThrowsArgumentException()
    {
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() =>
            MaturityCalculator.Calculate(
                castAt,
                Array.Empty<TemperatureReading>(),
                castAt.AddHours(24),
                new MaturitySettings()));
    }

    [Fact]
    public void Calculate_DuplicateTimestamps_ThrowsArgumentException()
    {
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 70.0),
            new(castAt.AddHours(24), 80.0),
            new(castAt.AddHours(24), 85.0),
            new(castAt.AddHours(48), 75.0)
        };

        Assert.Throws<ArgumentException>(() =>
            MaturityCalculator.Calculate(
                castAt,
                readings,
                castAt.AddHours(48),
                new MaturitySettings()));
    }
}
