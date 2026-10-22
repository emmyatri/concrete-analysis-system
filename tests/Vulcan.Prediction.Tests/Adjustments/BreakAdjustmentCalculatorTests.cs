using Vulcan.Prediction.Adjustments;
using Xunit;

namespace Vulcan.Prediction.Tests.Adjustments;

public sealed class BreakAdjustmentCalculatorTests
{
    [Fact]
    public void ThreeDayAdjustment_ValidBreak_FitsMeasuredStrength()
    {
        const double t3 = 3.0;
        const double expectedFinalPsi = 6000.0;
        const double b = 0.85;
        const double originalA = 4.0;

        double unadjustedPsi =
            t3 / (originalA + b * t3) * expectedFinalPsi;

        double measuredPsi = unadjustedPsi * 1.10;

        double? adjustedA =
            BreakAdjustmentCalculator.CalculateThreeDayAdjustedA(
                t3,
                measuredPsi,
                unadjustedPsi,
                expectedFinalPsi,
                b);

        Assert.NotNull(adjustedA);

        double fittedPsi =
            t3 / (adjustedA.Value + b * t3) * expectedFinalPsi;

        Assert.Equal(measuredPsi, fittedPsi, 6);
    }

    [Theory]
    [InlineData(0.69)]
    [InlineData(1.31)]
    public void ThreeDayAdjustment_RatioOutsideRange_ReturnsNull(
        double ratio)
    {
        double? adjustedA =
            BreakAdjustmentCalculator.CalculateThreeDayAdjustedA(
                3.0,
                3000.0 * ratio,
                3000.0,
                6000.0,
                0.85);

        Assert.Null(adjustedA);
    }

    [Theory]
    [InlineData(0.70)]
    [InlineData(1.30)]
    public void ThreeDayAdjustment_RatioAtBoundary_IsAccepted(
        double ratio)
    {
        double? adjustedA =
            BreakAdjustmentCalculator.CalculateThreeDayAdjustedA(
                3.0,
                3000.0 * ratio,
                3000.0,
                6000.0,
                0.85);

        Assert.NotNull(adjustedA);
    }

    [Fact]
    public void ThreeDayAdjustment_NonPositiveCoefficient_ReturnsNull()
    {
        double? adjustedA =
            BreakAdjustmentCalculator.CalculateThreeDayAdjustedA(
                3.0,
                6000.0,
                6000.0,
                6000.0,
                1.0);

        Assert.Null(adjustedA);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void ThreeDayAdjustment_InvalidMeasuredPsi_ReturnsNull(
        double measuredPsi)
    {
        double? adjustedA =
            BreakAdjustmentCalculator.CalculateThreeDayAdjustedA(
                3.0,
                measuredPsi,
                3000.0,
                6000.0,
                0.85);

        Assert.Null(adjustedA);
    }
    
    [Fact]
    public void ThreeAndSevenDayAdjustment_ValidBreaks_FitsBothMeasurements()
    {
        const double t3 = 3.0;
        const double t7 = 7.0;
        const double b = 0.85;
    
        const double measuredDay3Psi = 3000.0;
        const double measuredDay7Psi = 4200.0;
    
        var result =
            BreakAdjustmentCalculator.CalculateThreeAndSevenDayAdjustment(
                t3,
                t7,
                measuredDay3Psi,
                measuredDay7Psi,
                b);
    
        Assert.NotNull(result);
    
        double adjustedA = result.Value.AdjustedA;
        double referenceStrengthPsi = result.Value.ReferenceStrengthPsi;
    
        double fittedDay3Psi =
            t3 / (adjustedA + b * t3) * referenceStrengthPsi;
    
        double fittedDay7Psi =
            t7 / (adjustedA + b * t7) * referenceStrengthPsi;
    
        Assert.Equal(measuredDay3Psi, fittedDay3Psi, 6);
        Assert.Equal(measuredDay7Psi, fittedDay7Psi, 6);
    }
    
    [Fact]
    public void ThreeAndSevenDayAdjustment_InvalidReferenceStrength_ReturnsNull()
    {
        var result =
            BreakAdjustmentCalculator.CalculateThreeAndSevenDayAdjustment(
                3.0,
                7.0,
                3000.0,
                10000.0,
                0.85);
    
        Assert.Null(result);
    }
    
    [Fact]
    public void ThreeAndSevenDayAdjustment_NonPositiveAdjustedA_ReturnsNull()
    {
        var result =
            BreakAdjustmentCalculator.CalculateThreeAndSevenDayAdjustment(
                3.0,
                7.0,
                4000.0,
                4000.0,
                0.85);
    
        Assert.Null(result);
    }
    
    [Theory]
    [InlineData(0.0, 7.0)]
    [InlineData(3.0, 3.0)]
    [InlineData(7.0, 3.0)]
    [InlineData(double.NaN, 7.0)]
    [InlineData(3.0, double.PositiveInfinity)]
    public void ThreeAndSevenDayAdjustment_InvalidEquivalentAges_ReturnsNull(
        double t3,
        double t7)
    {
        var result =
            BreakAdjustmentCalculator.CalculateThreeAndSevenDayAdjustment(
                t3,
                t7,
                3000.0,
                4200.0,
                0.85);
    
        Assert.Null(result);
    }
    
    [Theory]
    [InlineData(0.0, 4200.0)]
    [InlineData(3000.0, 0.0)]
    [InlineData(double.NaN, 4200.0)]
    [InlineData(3000.0, double.PositiveInfinity)]
    public void ThreeAndSevenDayAdjustment_InvalidMeasuredBreaks_ReturnsNull(
        double day3Psi,
        double day7Psi)
    {
        var result =
            BreakAdjustmentCalculator.CalculateThreeAndSevenDayAdjustment(
                3.0,
                7.0,
                day3Psi,
                day7Psi,
                0.85);
    
        Assert.Null(result);
    }
    
    [Fact]
    public void Calculate_NoBreaks_ReturnsUnadjustedEstimate()
    {
        var result = BreakAdjustmentCalculator.Calculate(
            equivalentAgeDays: 28,
            unadjustedPsi: 6000,
            originalA: 4.0,
            b: 0.85,
            expectedFinalBreakPsi: 6000,
            equivalentAgeDay3: null,
            measuredDay3Psi: null,
            equivalentAgeDay7: null,
            measuredDay7Psi: null,
            unadjustedDay3Psi: 2750);
    
        Assert.Equal(BreakAdjustmentStage.None, result.Stage);
        Assert.Equal(6000, result.UnadjustedPsi);
        Assert.Equal(6000, result.AdjustedPsi);
        Assert.Null(result.FallbackReason);
    }
    
    [Fact]
    public void Calculate_ThreeDayBreak_UsesThreeDayAdjustment()
    {
        const double originalA = 4.0;
        const double b = 0.85;
        const double expectedFinalPsi = 6000;
    
        double unadjustedDay3 =
            3.0 / (originalA + b * 3.0) * expectedFinalPsi;
    
        double measuredDay3 = unadjustedDay3 * 1.1;
    
        var result = BreakAdjustmentCalculator.Calculate(
            equivalentAgeDays: 7,
            unadjustedPsi: 7.0 / (originalA + b * 7.0) * expectedFinalPsi,
            originalA: originalA,
            b: b,
            expectedFinalBreakPsi: expectedFinalPsi,
            equivalentAgeDay3: 3,
            measuredDay3Psi: measuredDay3,
            equivalentAgeDay7: null,
            measuredDay7Psi: null,
            unadjustedDay3Psi: unadjustedDay3);
    
        Assert.Equal(BreakAdjustmentStage.ThreeDay, result.Stage);
        Assert.True(result.AdjustedPsi > result.UnadjustedPsi);
        Assert.Null(result.FallbackReason);
    }
    
    [Fact]
    public void Calculate_TwoBreaks_UsesThreeAndSevenDayAdjustment()
    {
        var result = BreakAdjustmentCalculator.Calculate(
            equivalentAgeDays: 28,
            unadjustedPsi: 6000,
            originalA: 4.0,
            b: 0.85,
            expectedFinalBreakPsi: 6000,
            equivalentAgeDay3: 3,
            measuredDay3Psi: 3000,
            equivalentAgeDay7: 7,
            measuredDay7Psi: 4200,
            unadjustedDay3Psi: 3000);
    
        Assert.Equal(BreakAdjustmentStage.ThreeAndSevenDay, result.Stage);
        Assert.NotEqual(result.UnadjustedPsi, result.AdjustedPsi);
        Assert.Null(result.FallbackReason);
    }
    
    [Fact]
    public void Calculate_InvalidTwoBreakFit_FallsBackToThreeDay()
    {
        const double originalA = 4.0;
        const double b = 0.85;
        const double expectedFinalPsi = 6000;
    
        double unadjustedDay3 =
            3.0 / (originalA + b * 3.0) * expectedFinalPsi;
    
        var result = BreakAdjustmentCalculator.Calculate(
            equivalentAgeDays: 28,
            unadjustedPsi: 28.0 / (originalA + b * 28.0) * expectedFinalPsi,
            originalA: originalA,
            b: b,
            expectedFinalBreakPsi: expectedFinalPsi,
            equivalentAgeDay3: 3,
            measuredDay3Psi: unadjustedDay3,
            equivalentAgeDay7: 7,
            measuredDay7Psi: unadjustedDay3,
            unadjustedDay3Psi: unadjustedDay3);
    
        Assert.Equal(BreakAdjustmentStage.ThreeDay, result.Stage);
        Assert.NotNull(result.FallbackReason);
    }
    
    [Fact]
    public void Calculate_Day3BreakFarFromEstimate_ReturnsUnadjusted()
    {
        var result = BreakAdjustmentCalculator.Calculate(
            equivalentAgeDays: 28,
            unadjustedPsi: 6000,
            originalA: 4.0,
            b: 0.85,
            expectedFinalBreakPsi: 6000,
            equivalentAgeDay3: 3,
            measuredDay3Psi: 4500,
            equivalentAgeDay7: null,
            measuredDay7Psi: null,
            unadjustedDay3Psi: 3000);
    
        Assert.Equal(BreakAdjustmentStage.None, result.Stage);
        Assert.Equal(result.UnadjustedPsi, result.AdjustedPsi);
        Assert.Equal(
            "3-day breaks are far from the estimate",
            result.FallbackReason);
    }
}