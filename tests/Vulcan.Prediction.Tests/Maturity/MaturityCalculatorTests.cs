using Vulcan.Prediction.Maturity;

namespace Vulcan.Prediction.Tests.Maturity;

public class MaturityCalculatorTests
{
    [Fact]
    public void Calculate_ConstantReferenceTemperature_ReturnsThreeEquivalentDays()
    {
        // Arrange
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

        // Assert
        Assert.Equal(2980.8, result.MaturityFHours, precision: 6);
        Assert.Equal(3.0, result.EquivalentAgeDays, precision: 6);
    }
    
    [Fact]
    public void Calculate_AsOfTarget_IgnoresReadingsAfterTarget()
    {
        // Arrange
        var castAt = new DateTime(
            2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        var targetTime = castAt.AddHours(72);

        var readings = new List<TemperatureReading>
        {
            new(castAt, 70.0),
            new(castAt.AddHours(24), 80.0),
            new(castAt.AddHours(48), 60.0),
            new(castAt.AddHours(96), 100.0) // After target: must be ignored.
        };

        var settings = new MaturitySettings();

        // Act
        var result = MaturityCalculator.Calculate(
            castAt,
            readings,
            targetTime,
            settings,
            MaturityCalculationMode.AsOfTarget);

        // Assert
        Assert.Equal(2856.0, result.MaturityFHours, precision: 6);
        Assert.Equal(2856.0 / 993.6, result.EquivalentAgeDays, precision: 6);
    }
    
    [Fact]
    public void Calculate_Retrospective_InterpolatesAtTargetTime()
    {
        // Arrange
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

        // Assert
        Assert.Equal(1548.0, result.MaturityFHours, precision: 6);
        Assert.Equal(1548.0 / 993.6, result.EquivalentAgeDays, precision: 6);
    }
    
    [Fact]
    public void Calculate_BelowDatumTemperature_DoesNotAccumulateMaturity()
    {
        // Arrange
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

        // Assert
        Assert.Equal(0.0, result.MaturityFHours, precision: 6);
        Assert.Equal(0.0, result.EquivalentAgeDays, precision: 6);
    }
    
    [Fact]
    public void Calculate_RetrospectiveWithoutFutureReading_MatchesAsOfTarget()
    {
        // Arrange
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

        // Assert
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
    
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Calculate_InvalidTemperature_ThrowsArgumentException(
        double invalidTemperature)
    {
        // Arrange
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
        // Arrange
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
        // Arrange
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
        // Arrange
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
        // Arrange
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
}
