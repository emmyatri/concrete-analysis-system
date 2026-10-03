using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Interfaces;
using Vulcan.Domain.Services;
using Vulcan.Domain.Validation;
using static Vulcan.Infrastructure.SetupUtils;

namespace Vulcan.Domain.Tests;

public class CompositionValidatorTests
{
    private readonly CompositionValidator _validator;

    public CompositionValidatorTests()
    {
        // Use the same validation limits loaded by the application.
        // This keeps the configured ranges in one source of truth rather
        // than duplicating M1-10 values inside the composition validator.
        _validator = new CompositionValidator(
            new LimitEvaluator(
                new FakeLimitProvider([.. LoadValidationLimits()])));
    }

    // -------------------------------------------------------------------------
    // Cement
    // The configured minimum and maximum are inclusive.
    // Values immediately outside either boundary must be rejected.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("10", true)]
    [InlineData("15", true)]
    [InlineData("9.99", false)]
    [InlineData("15.01", false)]
    public void Cement_Range_IsValidated(string value, bool expected)
    {
        Assert.Equal(
            expected,
            IsLimitValid(LimitKeys.CementPct, decimal.Parse(value)));
    }

    // -------------------------------------------------------------------------
    // Fine aggregate / sand
    // The configured minimum and maximum are inclusive.
    // Values immediately outside either boundary must be rejected.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("25", true)]
    [InlineData("30", true)]
    [InlineData("24.99", false)]
    [InlineData("30.01", false)]
    public void FineAggregate_Range_IsValidated(string value, bool expected)
    {
        Assert.Equal(
            expected,
            IsLimitValid(LimitKeys.FineAggregatePct, decimal.Parse(value)));
    }

    // -------------------------------------------------------------------------
    // Coarse aggregate / gravel
    // The configured minimum and maximum are inclusive.
    // Values immediately outside either boundary must be rejected.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("40", true)]
    [InlineData("55", true)]
    [InlineData("39.99", false)]
    [InlineData("55.01", false)]
    public void CoarseAggregate_Range_IsValidated(string value, bool expected)
    {
        Assert.Equal(
            expected,
            IsLimitValid(LimitKeys.CoarseAggregatePct, decimal.Parse(value)));
    }

    // -------------------------------------------------------------------------
    // Water
    // The configured minimum and maximum are inclusive.
    // Values immediately outside either boundary must be rejected.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("15", true)]
    [InlineData("20", true)]
    [InlineData("14.99", false)]
    [InlineData("20.01", false)]
    public void Water_Range_IsValidated(string value, bool expected)
    {
        Assert.Equal(
            expected,
            IsLimitValid(LimitKeys.WaterPct, decimal.Parse(value)));
    }

    // -------------------------------------------------------------------------
    // SCM
    // SCM may range from 0 through 100.
    // Zero SCM requires None; positive SCM requires a supported SCM type.
    // -------------------------------------------------------------------------

    [Fact]
    public void Scm_AtZeroWithNone_IsValid()
    {
        Assert.True(_validator.IsScmValid(0m, ScmType.None));
    }

    [Fact]
    public void Scm_AtZeroWithType_IsInvalid()
    {
        Assert.False(_validator.IsScmValid(0m, ScmType.FlyAsh));
    }

    [Theory]
    [InlineData(ScmType.FlyAsh)]
    [InlineData(ScmType.Slag)]
    [InlineData(ScmType.SilicaFume)]
    public void Scm_AboveZeroWithSupportedType_IsValid(ScmType scmType)
    {
        Assert.True(_validator.IsScmValid(0.01m, scmType));
    }

    [Fact]
    public void Scm_AboveZeroWithNone_IsInvalid()
    {
        Assert.False(_validator.IsScmValid(0.01m, ScmType.None));
    }

    [Fact]
    public void Scm_AtMaximum_IsValid()
    {
        Assert.True(_validator.IsScmValid(100m, ScmType.FlyAsh));
    }

    [Fact]
    public void Scm_JustBelowMinimum_IsInvalid()
    {
        Assert.False(_validator.IsScmValid(-0.01m, ScmType.None));
    }

    [Fact]
    public void Scm_JustAboveMaximum_IsInvalid()
    {
        Assert.False(_validator.IsScmValid(100.01m, ScmType.FlyAsh));
    }

    [Fact]
    public void Scm_WithUnsupportedType_IsInvalid()
    {
        Assert.False(
            _validator.IsScmValid(10m, (ScmType)999));
    }

    // -------------------------------------------------------------------------
    // Total composition
    // All five percentages are included in the total.
    // Exactly 100.00% is valid; 100.01% must be rejected.
    // -------------------------------------------------------------------------

    [Fact]
    public void Total_At100Percent_IsValid()
    {
        var composition = CreateComposition(
            cementPct: 15m,
            scmPct: 5m,
            coarseAggregatePct: 40m,
            fineAggregatePct: 25m,
            waterPct: 15m);

        Assert.Equal(
            100m,
            CompositionValidator.CalculateTotal(composition));

        Assert.True(
            _validator.IsTotalValid(composition));
    }

    [Fact]
    public void Total_At100Point01Percent_IsInvalid()
    {
        var composition = CreateComposition(
            cementPct: 15m,
            scmPct: 5m,
            coarseAggregatePct: 40m,
            fineAggregatePct: 25m,
            waterPct: 15.01m);

        Assert.Equal(
            100.01m,
            CompositionValidator.CalculateTotal(composition));

        Assert.False(
            _validator.IsTotalValid(composition));
    }

    [Fact]
    public void Validate_WhenTotalExceeds100_MessageIncludesTotal()
    {
        var composition = CreateComposition(
            cementPct: 15m,
            scmPct: 5m,
            coarseAggregatePct: 40m,
            fineAggregatePct: 25m,
            waterPct: 15.01m);

        var errors = _validator.Validate(composition);

        Assert.Contains(
            "Mix percentages total 100.01%, which exceeds 100%.",
            errors);
    }

    // -------------------------------------------------------------------------
    // Air / Other
    // This value is calculated from the unused percentage for display.
    // It is not a stored field on Composition.
    // -------------------------------------------------------------------------

    [Fact]
    public void AirOther_IsCalculatedFromRemainingPercentage()
    {
        var composition = CreateComposition(
            cementPct: 10m,
            scmPct: 0m,
            coarseAggregatePct: 40m,
            fineAggregatePct: 25m,
            waterPct: 15m);

        Assert.Equal(
            10m,
            CompositionValidator.CalculateAirOther(composition));
    }

    [Fact]
    public void AirOther_At100PercentTotal_IsZero()
    {
        var composition = CreateComposition(
            cementPct: 15m,
            scmPct: 5m,
            coarseAggregatePct: 40m,
            fineAggregatePct: 25m,
            waterPct: 15m);

        Assert.Equal(
            0m,
            CompositionValidator.CalculateAirOther(composition));
    }

    // -------------------------------------------------------------------------
    // Decimal precision
    // Every entered mix percentage may contain at most two decimal places.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("0")]
    [InlineData("0.01")]
    [InlineData("10")]
    [InlineData("10.1")]
    [InlineData("10.12")]
    [InlineData("100.00")]
    public void Percentage_WithAtMostTwoDecimalPlaces_IsValid(string value)
    {
        Assert.True(
            CompositionValidator.HasAtMostTwoDecimalPlaces(
                decimal.Parse(value)));
    }

    [Theory]
    [InlineData("0.001")]
    [InlineData("10.123")]
    [InlineData("99.999")]
    public void Percentage_WithMoreThanTwoDecimalPlaces_IsInvalid(string value)
    {
        Assert.False(
            CompositionValidator.HasAtMostTwoDecimalPlaces(
                decimal.Parse(value)));
    }

    // -------------------------------------------------------------------------
    // Complete validation
    // A valid composition returns no errors. Invalid compositions return all
    // applicable problems at once rather than stopping at the first failure.
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_WithValidComposition_ReturnsNoErrors()
    {
        var composition = CreateComposition(
            cementPct: 12m,
            scmPct: 3m,
            coarseAggregatePct: 40m,
            fineAggregatePct: 25m,
            waterPct: 15m);

        var errors = _validator.Validate(composition);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WithMultipleProblems_ReturnsAllErrors()
    {
        var composition = CreateComposition(
            cementPct: 15.123m,
            scmPct: 5m,
            coarseAggregatePct: 39m,
            fineAggregatePct: 25m,
            waterPct: 16m);

        var errors = _validator.Validate(composition);

        Assert.Contains(
            "Cement composition must be within the range [10-15]",
            errors);

        Assert.Contains(
            "Coarse aggregate composition must be within the range [40-55]",
            errors);

        Assert.Contains(
            "Cement percentage cannot have more than two decimal places.",
            errors);

        Assert.Contains(
            "Mix percentages total 100.123%, which exceeds 100%.",
            errors);
    }

    // -------------------------------------------------------------------------
    // Test helpers
    // -------------------------------------------------------------------------

    private bool IsLimitValid(string key, decimal value)
    {
        // Start with a valid composition, then replace only the field being
        // tested. This isolates each configured range from unrelated rules.
        var composition = CreateComposition(
            cementPct: 12m,
            scmPct: 0m,
            coarseAggregatePct: 40m,
            fineAggregatePct: 25m,
            waterPct: 15m);

        switch (key)
        {
            case LimitKeys.CementPct:
                composition.CementPct = value;
                break;

            case LimitKeys.FineAggregatePct:
                composition.FineAggregatePct = value;
                break;

            case LimitKeys.CoarseAggregatePct:
                composition.CoarseAggregatePct = value;
                break;

            case LimitKeys.WaterPct:
                composition.WaterPct = value;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(key));
        }

        var errors = _validator.Validate(composition);

        // The messages come from the same seeded validation limits used by
        // the application. No range values are recreated in the validator.
        return key switch
        {
            LimitKeys.CementPct =>
                !errors.Contains(
                    "Cement composition must be within the range [10-15]"),

            LimitKeys.FineAggregatePct =>
                !errors.Contains(
                    "Fine aggregate composition must be within the range [25-30]"),

            LimitKeys.CoarseAggregatePct =>
                !errors.Contains(
                    "Coarse aggregate composition must be within the range [40-55]"),

            LimitKeys.WaterPct =>
                !errors.Contains(
                    "Water composition must be within the range [15-20]"),

            _ => throw new ArgumentOutOfRangeException(nameof(key))
        };
    }

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
            ScmType = scmPct == 0m
                ? ScmType.None
                : ScmType.FlyAsh,
            CoarseAggregatePct = coarseAggregatePct,
            FineAggregatePct = fineAggregatePct,
            WaterPct = waterPct
        };
    }

    // Lightweight provider used only to supply the real M1-10 seed limits
    // to LimitEvaluator without requiring a database in these unit tests.
    private sealed class FakeLimitProvider(params ValidationLimit[] limits)
        : ILimitProvider
    {
        public LimitSet Get(string key)
        {
            var matches = limits
                .Where(limit => limit.Key == key)
                .ToList();

            if (matches.Count == 0)
            {
                throw new KeyNotFoundException(key);
            }

            return new LimitSet
            {
                Warn = matches.FirstOrDefault(
                    limit => limit.Severity == Severity.Warn),

                Reject = matches.FirstOrDefault(
                    limit => limit.Severity == Severity.Reject)
            };
        }
    }
}
