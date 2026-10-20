
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
        // Arrange: 73.4°F for 72 hours should equal 3 days at reference temperature.
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

        // Assert: (73.4 - 32) * 72 = 2980.8 °F·h.
        Assert.Equal(2980.8, result.MaturityFHours, precision: 6);
        Assert.Equal(3.0, result.EquivalentAgeDays, precision: 6);
    }


    [Fact]
    public void Calculate_PublishedNdotExample_ReturnsExpectedMaturity()
    {
        // Arrange
        // Nebraska DOT published Nurse-Saul example:
        // Temperature increases from 20°C to 50°C over 3 hours.
        // Datum temperature: -10°C.
        // Published maturity: 135°C·h = 243°F·h.
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

        // Act
        var result = MaturityCalculator.Calculate(
            castAt,
            readings,
            targetTime,
            settings);

        // Assert
        // Average temperature: (68 + 122) / 2 = 95°F.
        // Maturity: (95 - 14) * 3 = 243°F·h.
        // Equivalent age: 243 / ((73.4 - 14) * 24).

        Assert.Equal(243.0, result.MaturityFHours, precision: 6);

        Assert.Equal(
            243.0 / ((73.4 - 14.0) * 24.0),
            result.EquivalentAgeDays,
            precision: 6);
    }

    [Fact]
    public void Calculate_BelowDatumTemperature_DoesNotAccumulateMaturity()
    {
        // Arrange: All readings are below the 32°F datum.
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var targetTime = castAt.AddHours(48);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 20.0),
            new(castAt.AddHours(24), 25.0),
            new(targetTime, 30.0)
        };

        var settings = new MaturitySettings();

        // Act
        var result = MaturityCalculator.Calculate(
            castAt,
            readings,
            targetTime,
            settings);

        // Assert: Temperatures below the datum contribute zero maturity.
        Assert.Equal(0.0, result.MaturityFHours, precision: 6);
        Assert.Equal(0.0, result.EquivalentAgeDays, precision: 6);
    }

    [Fact]
    public void Calculate_VmSyn006_D28_1_ReturnsExpectedDay3Maturity()
    {
        // Arrange: Actual VM-SYN-006 / D28-1 synthetic fixture readings.
        var castAt = new DateTime(
            2026, 6, 21, 12, 0, 0, DateTimeKind.Utc);

        var targetTime = castAt.AddDays(3);

        // Seven readings, spaced 12 hours apart.
        var temperatures = new[]
        {
            78.3, 81.1, 82.9, 80.7, 78.4, 77.9, 76.1
        };

        var readings = temperatures
            .Select((temp, index) => new TemperatureReading(
                castAt.AddHours(index * 12),
                temp))
            .ToList();

        var settings = new MaturitySettings();

        // Act
        var result = MaturityCalculator.Calculate(
            castAt,
            readings,
            targetTime,
            settings);

        // Assert: Acceptance values specified in SCRUM-160.
        Assert.Equal(3434.4, result.MaturityFHours, precision: 6);
        Assert.Equal(3434.4 / 993.6, result.EquivalentAgeDays, precision: 6);
    }

    // ============================================================
    // Historical Target Calculations
    // ============================================================

    [Fact]
    public void Calculate_AsOfTarget_IgnoresReadingsAfterTarget()
    {
        // Arrange: Day-4 reading must not influence the day-3 calculation.
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var targetTime = castAt.AddHours(72);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 70.0),
            new(castAt.AddHours(24), 80.0),
            new(castAt.AddHours(48), 60.0),
            new(castAt.AddHours(96), 100.0) // Beyond target.
        };

        var settings = new MaturitySettings();

        // Act
        var result = MaturityCalculator.Calculate(
            castAt,
            readings,
            targetTime,
            settings,
            MaturityCalculationMode.AsOfTarget);

        // Assert: Only readings available by hour 72 contribute.
        Assert.Equal(2856.0, result.MaturityFHours, precision: 6);
        Assert.Equal(2856.0 / 993.6, result.EquivalentAgeDays, precision: 6);
    }

    [Fact]
    public void Calculate_Day7Target_IgnoresReadingsAfterTarget()
    {
        // Arrange: Constant reference temperature through day 7.
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

            // Future readings must not affect the historical result.
            new(castAt.AddDays(8), 100.0),
            new(castAt.AddDays(10), 120.0)
        };

        var settings = new MaturitySettings();

        // Act
        var result = MaturityCalculator.Calculate(
            castAt,
            readings,
            targetTime,
            settings);

        // Assert: (73.4 - 32) * 168 = 6955.2 °F·h.
        Assert.Equal(6955.2, result.MaturityFHours, precision: 6);
        Assert.Equal(7.0, result.EquivalentAgeDays, precision: 6);
    }

    // ============================================================
    // Calculation Modes
    // ============================================================

    [Fact]
    public void Calculate_Retrospective_InterpolatesAtTargetTime()
    {
        // Arrange: Target is halfway between readings at hours 24 and 48.
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var targetTime = castAt.AddHours(36);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 60.0),
            new(castAt.AddHours(24), 80.0),
            new(castAt.AddHours(48), 100.0)
        };

        var settings = new MaturitySettings();

        // Act
        var result = MaturityCalculator.Calculate(
            castAt,
            readings,
            targetTime,
            settings,
            MaturityCalculationMode.Retrospective);

        // Assert: Interpolate 90°F at hour 36 and clip the interval.
        Assert.Equal(1548.0, result.MaturityFHours, precision: 6);
        Assert.Equal(1548.0 / 993.6, result.EquivalentAgeDays, precision: 6);
    }

    [Fact]
    public void Calculate_RetrospectiveWithoutFutureReading_MatchesAsOfTarget()
    {
        // Arrange: No reading exists after the requested target.
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var targetTime = castAt.AddHours(36);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 60.0),
            new(castAt.AddHours(24), 80.0)
        };

        var settings = new MaturitySettings();

        // Act
        var asOfTarget = MaturityCalculator.Calculate(
            castAt,
            readings,
            targetTime,
            settings,
            MaturityCalculationMode.AsOfTarget);

        var retrospective = MaturityCalculator.Calculate(
            castAt,
            readings,
            targetTime,
            settings,
            MaturityCalculationMode.Retrospective);

        // Assert: Both modes use the same fallback without future data.
        Assert.Equal(1368.0, retrospective.MaturityFHours, precision: 6);

        Assert.Equal(
            asOfTarget.MaturityFHours,
            retrospective.MaturityFHours,
            precision: 6);

        Assert.Equal(
            asOfTarget.EquivalentAgeDays,
            retrospective.EquivalentAgeDays,
            precision: 6);
    }

    // ============================================================
    // Reading Order and Time Boundaries
    // ============================================================

    [Fact]
    public void Calculate_FirstReadingAfterCasting_UsesFirstTemperatureForInitialInterval()
    {
        // Arrange: The first valid reading occurs 12 hours after casting.
        // A reading before casting must be ignored.
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var targetTime = castAt.AddHours(24);

        var readings = new List<TemperatureReading>
        {
            new(castAt.AddHours(-6), 100.0), // Before casting: ignored.
            new(castAt.AddHours(12), 70.0),
            new(targetTime, 80.0)
        };

        var settings = new MaturitySettings();

        // Act
        var result = MaturityCalculator.Calculate(
            castAt,
            readings,
            targetTime,
            settings);

        // Assert
        // Hours 0-12:  (70 - 32) * 12 = 456.0 °F·h
        // Hours 12-24: (75 - 32) * 12 = 516.0 °F·h
        // Total: 972.0 °F·h
        Assert.Equal(972.0, result.MaturityFHours, precision: 6);
        Assert.Equal(972.0 / 993.6, result.EquivalentAgeDays, precision: 6);
    }

    [Fact]
    public void Calculate_OutOfOrderReadings_ReturnsSameMaturity()
    {
        // Arrange: The calculator should sort readings chronologically.
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

        // Act
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

        // Assert: Input ordering must not change maturity or equivalent age.
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
        // Arrange: No time has elapsed since casting.
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 75.0)
        };

        var settings = new MaturitySettings();

        // Act
        var result = MaturityCalculator.Calculate(
            castAt,
            readings,
            castAt,
            settings);

        // Assert: Zero elapsed hours means zero accumulated maturity.
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
        // Arrange: Non-finite temperatures cannot be used in calculations.
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 73.4),
            new(castAt.AddHours(24), invalidTemperature)
        };

        var settings = new MaturitySettings();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            MaturityCalculator.Calculate(
                castAt,
                readings,
                castAt.AddHours(48),
                settings));
    }

    [Fact]
    public void Calculate_NonUtcReading_ThrowsArgumentException()
    {
        // Arrange: All reading timestamps must use UTC.
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 73.4),
            new(
                new DateTime(2026, 10, 21, 8, 0, 0, DateTimeKind.Local),
                75.0)
        };

        var settings = new MaturitySettings();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            MaturityCalculator.Calculate(
                castAt,
                readings,
                castAt.AddHours(48),
                settings));
    }

    [Fact]
    public void Calculate_TargetBeforeCasting_ThrowsArgumentException()
    {
        // Arrange: Maturity cannot be calculated before casting.
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var targetTime = castAt.AddHours(-1);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 73.4)
        };

        var settings = new MaturitySettings();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            MaturityCalculator.Calculate(
                castAt,
                readings,
                targetTime,
                settings));
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
        // Arrange: Reference temperature must exceed datum; both must be finite.
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

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            MaturityCalculator.Calculate(
                castAt,
                readings,
                castAt.AddHours(48),
                settings));
    }

    [Fact]
    public void Calculate_NoApplicableReadings_ThrowsArgumentException()
    {
        // Arrange: All readings fall outside the cast-to-target window.
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var targetTime = castAt.AddHours(72);

        var readings = new List<TemperatureReading>
        {
            new(castAt.AddHours(-1), 70.0),
            new(castAt.AddHours(96), 80.0)
        };

        var settings = new MaturitySettings();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            MaturityCalculator.Calculate(
                castAt,
                readings,
                targetTime,
                settings));
    }

    [Fact]
    public void Calculate_DuplicateTimestamps_ThrowsArgumentException()
    {
        // Arrange: Conflicting temperatures at the same timestamp are ambiguous.
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var targetTime = castAt.AddHours(48);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 70.0),
            new(castAt.AddHours(24), 80.0),
            new(castAt.AddHours(24), 85.0),
            new(targetTime, 75.0)
        };

        var settings = new MaturitySettings();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            MaturityCalculator.Calculate(
                castAt,
                readings,
                targetTime,
                settings));
    }
}
