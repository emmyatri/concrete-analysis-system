using Vulcan.Prediction.Adjustments;
using Xunit;

namespace Vulcan.Prediction.Tests.Adjustments;

public sealed class BreakMeasurementAggregatorTests
{
    private static readonly StrengthBreakMeasurement[] _measurements =
    [
        new("S1", 3, 3000, false),
        new("S2", 3, 3200, false),
        new("S3", 7, 4200, false),
        new("S4", 7, 4400, false),
        new("S5", 28, 6000, false)
    ];

    [Theory]
    [InlineData(3, 3100)]
    [InlineData(7, 4300)]
    [InlineData(28, 6000)]
    public void GetAveragePsi_CurrentCurve_AveragesBreaks(
        int day,
        double expectedPsi)
    {
        double? result = BreakMeasurementAggregator.GetAveragePsi(
            _measurements,
            day);

        Assert.Equal(expectedPsi, result);
    }

    [Fact]
    public void GetAveragePsi_HistoricalDay7_UsesDay3Only()
    {
        double? day3 = BreakMeasurementAggregator.GetAveragePsi(
            _measurements,
            day: 3,
            predictionDay: 7);

        double? day7 = BreakMeasurementAggregator.GetAveragePsi(
            _measurements,
            day: 7,
            predictionDay: 7);

        Assert.Equal(3100, day3);
        Assert.Null(day7);
    }

    [Fact]
    public void GetAveragePsi_HistoricalDay28_UsesEarlierBreaks()
    {
        Assert.Equal(3100, BreakMeasurementAggregator.GetAveragePsi(
            _measurements, 3, predictionDay: 28));

        Assert.Equal(4300, BreakMeasurementAggregator.GetAveragePsi(
            _measurements, 7, predictionDay: 28));

        Assert.Null(BreakMeasurementAggregator.GetAveragePsi(
            _measurements, 28, predictionDay: 28));
    }

    [Fact]
    public void GetAveragePsi_NoMatchingBreaks_ReturnsNull()
    {
        double? result = BreakMeasurementAggregator.GetAveragePsi(
            _measurements,
            day: 14);

        Assert.Null(result);
    }

    [Fact]
    public void GetAveragePsi_EmptyMeasurements_ReturnsNull()
    {
        double? result = BreakMeasurementAggregator.GetAveragePsi(
            Array.Empty<StrengthBreakMeasurement>(),
            day: 3);

        Assert.Null(result);
    }

    [Fact]
    public void GetAveragePsi_NullMeasurements_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            BreakMeasurementAggregator.GetAveragePsi(null!, day: 3));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-100.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void GetAveragePsi_InvalidStrength_Throws(double strengthPsi)
    {
        StrengthBreakMeasurement[] measurements =
        [
            new("S1", 3, strengthPsi, false)
        ];

        Assert.Throws<ArgumentException>(() =>
            BreakMeasurementAggregator.GetAveragePsi(measurements, 3));
    }

    [Fact]
    public void GetAveragePsi_DuplicateSpecimen_Throws()
    {
        StrengthBreakMeasurement[] measurements =
        [
            new("S1", 3, 3000, false),
            new("s1", 3, 3200, false)
        ];

        Assert.Throws<ArgumentException>(() =>
            BreakMeasurementAggregator.GetAveragePsi(measurements, 3));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void GetAveragePsi_MissingSpecimenId_Throws(string specimenId)
    {
        StrengthBreakMeasurement[] measurements =
        [
            new(specimenId, 3, 3000, false)
        ];

        Assert.Throws<ArgumentException>(() =>
            BreakMeasurementAggregator.GetAveragePsi(measurements, 3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(14)]
    [InlineData(29)]
    public void GetAveragePsi_UnsupportedBreakAge_Throws(int day)
    {
        StrengthBreakMeasurement[] measurements =
        [
            new("S1", day, 3000, false)
        ];

        Assert.Throws<ArgumentException>(() =>
            BreakMeasurementAggregator.GetAveragePsi(measurements, 3));
    }

    [Fact]
    public void GetAveragePsi_NullEntry_Throws()
    {
        StrengthBreakMeasurement[] measurements =
        [
            new("S1", 3, 3000, false),
            null!
        ];

        Assert.Throws<ArgumentException>(() =>
            BreakMeasurementAggregator.GetAveragePsi(measurements, 3));
    }

    [Fact]
    public void GetAveragePsi_InvalidLaterAge_StillThrows()
    {
        StrengthBreakMeasurement[] measurements =
        [
            new("S1", 3, 3000, false),
            new("S2", 7, -100, false)
        ];

        Assert.Throws<ArgumentException>(() =>
            BreakMeasurementAggregator.GetAveragePsi(
                measurements,
                day: 3,
                predictionDay: 7));
    }
}
