using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Services;

namespace Vulcan.Domain.Tests;

public class CompositionValidatorTests
{
    // SCM percentage may range from 0 through 100, inclusive.
    // When SCM is zero, the SCM type must be None.
    [Fact]
    public void Scm_AtZeroWithNone_IsValid()
    {
        Assert.True(CompositionValidator.IsScmValid(0m, ScmType.None));
    }

    [Fact]
    public void Scm_AtZeroWithType_IsInvalid()
    {
        Assert.False(CompositionValidator.IsScmValid(0m, ScmType.FlyAsh));
    }

    // When SCM is greater than zero, a supported SCM type is required.
    [Theory]
    [InlineData(ScmType.FlyAsh)]
    [InlineData(ScmType.Slag)]
    [InlineData(ScmType.SilicaFume)]
    public void Scm_AboveZeroWithSupportedType_IsValid(ScmType scmType)
    {
        Assert.True(CompositionValidator.IsScmValid(0.01m, scmType));
    }

    // Zero SCM with a type of None satisfies the SCM relationship rule.
    [Fact]
    public void Validate_WithZeroScmAndNone_DoesNotReturnScmError()
    {
        var composition = new Composition
        {
            CementPct = 20m,
            ScmPct = 0m,
            ScmType = ScmType.None,
            CoarseAggregatePct = 35m,
            FineAggregatePct = 25m,
            WaterPct = 15m
        };

        var errors = CompositionValidator.Validate(composition);

        Assert.DoesNotContain(
            "SCM percentage and type are inconsistent.",
            errors);
    }

    [Fact]
    public void Scm_AboveZeroWithNone_IsInvalid()
    {
        Assert.False(CompositionValidator.IsScmValid(0.01m, ScmType.None));
    }

    [Fact]
    public void Scm_AtMaximum_IsValid()
    {
        Assert.True(CompositionValidator.IsScmValid(100m, ScmType.FlyAsh));
    }

    [Fact]
    public void Scm_JustBelowMinimum_IsInvalid()
    {
        Assert.False(CompositionValidator.IsScmValid(-0.01m, ScmType.None));
    }

    [Fact]
    public void Scm_JustAboveMaximum_IsInvalid()
    {
        Assert.False(CompositionValidator.IsScmValid(100.01m, ScmType.FlyAsh));
    }

    [Fact]
    public void Scm_WithUnsupportedType_IsInvalid()
    {
        var scmType = (ScmType)4;

        Assert.False(CompositionValidator.IsScmValid(10m, scmType));
    }

    // The total of all five mix percentages cannot exceed 100%.
    // Exactly 100% is valid, while anything above 100% must be rejected.
    [Fact]
    public void Total_At100Percent_IsValid()
    {
        var composition = CreateComposition(
            cementPct: 20m,
            scmPct: 10m,
            coarseAggregatePct: 30m,
            fineAggregatePct: 25m,
            waterPct: 15m);

        Assert.Equal(100m, CompositionValidator.CalculateTotal(composition));
        Assert.True(CompositionValidator.IsTotalValid(composition));
    }

    [Fact]
    public void Total_At100Point01Percent_IsInvalid()
    {
        var composition = CreateComposition(
            cementPct: 20m,
            scmPct: 10m,
            coarseAggregatePct: 30m,
            fineAggregatePct: 25m,
            waterPct: 15.01m);

        Assert.Equal(100.01m, CompositionValidator.CalculateTotal(composition));
        Assert.False(CompositionValidator.IsTotalValid(composition));
    }

    // Air / other is calculated for display only and represents the
    // percentage remaining after all five mix percentages are totaled.
    [Fact]
    public void AirOther_IsCalculatedFromRemainingPercentage()
    {
        var composition = CreateComposition(
            cementPct: 15m,
            scmPct: 5m,
            coarseAggregatePct: 40m,
            fineAggregatePct: 25m,
            waterPct: 10m);

        Assert.Equal(5m, CompositionValidator.CalculateAirOther(composition));
    }

    [Fact]
    public void AirOther_At100PercentTotal_IsZero()
    {
        var composition = CreateComposition(
            cementPct: 20m,
            scmPct: 10m,
            coarseAggregatePct: 30m,
            fineAggregatePct: 25m,
            waterPct: 15m);

        Assert.Equal(0m, CompositionValidator.CalculateAirOther(composition));
    }
    // Every mix percentage is limited to at most two decimal places.
    [Theory]
    [InlineData("0")]
    [InlineData("0.01")]
    [InlineData("10")]
    [InlineData("10.1")]
    [InlineData("10.12")]
    [InlineData("100.00")]
    public void Percentage_WithAtMostTwoDecimalPlaces_IsValid(string value)
    {
        var percentage = decimal.Parse(value);

        Assert.True(CompositionValidator.HasAtMostTwoDecimalPlaces(percentage));
    }

    [Theory]
    [InlineData("0.001")]
    [InlineData("10.123")]
    [InlineData("99.999")]
    public void Percentage_WithMoreThanTwoDecimalPlaces_IsInvalid(string value)
    {
        var percentage = decimal.Parse(value);

        Assert.False(CompositionValidator.HasAtMostTwoDecimalPlaces(percentage));
    }
    // Validation reports every composition problem at once instead of
    // stopping after the first failed rule.
    [Fact]
    public void Validate_WithMultipleProblems_ReturnsAllErrors()
    {
        var composition = CreateComposition(
            cementPct: 20.123m,
            scmPct: 10.123m,
            coarseAggregatePct: 30m,
            fineAggregatePct: 25m,
            waterPct: 15m);

        var errors = CompositionValidator.Validate(composition);

        Assert.Contains(
            "Cement percentage cannot have more than two decimal places.",
            errors);
        Assert.Contains(
            "SCM percentage cannot have more than two decimal places.",
            errors);
        Assert.Contains(
            "Mix percentages total 100.246%, which exceeds 100%.",
            errors);
    }
    // When the total exceeds 100%, the validation message includes
    // the calculated total so the operator can see how far over the limit it is.
    [Fact]
    public void Validate_WhenTotalExceeds100_MessageIncludesTotal()
    {
        var composition = CreateComposition(
            cementPct: 20m,
            scmPct: 10m,
            coarseAggregatePct: 30m,
            fineAggregatePct: 25m,
            waterPct: 15.01m);

        var errors = CompositionValidator.Validate(composition);

        Assert.Contains(
            "Mix percentages total 100.01%, which exceeds 100%.",
            errors);
    }
    // Helper
    private static Composition CreateComposition(
        decimal cementPct,
        decimal scmPct,
        decimal coarseAggregatePct,
        decimal fineAggregatePct,
        decimal waterPct)
    {
        return new Composition
        {
            CementPct = cementPct,
            ScmPct = scmPct,
            ScmType = scmPct == 0m ? ScmType.None : ScmType.FlyAsh,
            CoarseAggregatePct = coarseAggregatePct,
            FineAggregatePct = fineAggregatePct,
            WaterPct = waterPct
        };
    }
}
