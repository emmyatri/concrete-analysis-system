using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Interfaces;
using Vulcan.Domain.Services;
using Vulcan.Domain.Validation;
using static Vulcan.Infrastructure.SetupUtils;

namespace Vulcan.Domain.Tests;

public class WaterCementRatioCalculatorTests
{
    private readonly WaterCementRatioCalculator _calculator;

    public WaterCementRatioCalculatorTests()
    {
        _calculator = new WaterCementRatioCalculator(
            new FakeSpecificGravityProvider(),
            new LimitEvaluator(
                new FakeLimitProvider([.. LoadValidationLimits()])));
    }

    // -------------------------------------------------------------------------
    // Calculation
    // Water-to-cement ratio is calculated by weight after converting the
    // entered volume percentages using each material's specific gravity.
    // -------------------------------------------------------------------------

    [Fact]
    public void Calculate_WithNoScm_ReturnsWorkedExample()
    {
        var composition = CreateComposition(
            cementPct: 12m,
            waterPct: 18m,
            scmPct: 0m,
            scmType: ScmType.None);

        var ratio = _calculator.Calculate(composition);

        Assert.NotNull(ratio);
        Assert.Equal(0.48m, decimal.Round(ratio.Value, 2));
    }

    [Theory]
    [InlineData(ScmType.FlyAsh, "0.42")]
    [InlineData(ScmType.Slag, "0.41")]
    [InlineData(ScmType.SilicaFume, "0.42")]
    public void Calculate_WithScm_ReturnsExpectedRatio(
        ScmType scmType,
        string expectedRatio)
    {
        var composition = CreateComposition(
            cementPct: 12m,
            waterPct: 18m,
            scmPct: 2m,
            scmType: scmType);

        var ratio = _calculator.Calculate(composition);

        Assert.NotNull(ratio);
        Assert.Equal(
            decimal.Parse(expectedRatio),
            decimal.Round(ratio.Value, 2));
    }

    [Fact]
    public void Calculate_WithZeroCementitiousWeight_ReturnsNull()
    {
        var composition = CreateComposition(
            cementPct: 0m,
            waterPct: 18m,
            scmPct: 0m,
            scmType: ScmType.None);

        var ratio = _calculator.Calculate(composition);

        Assert.Null(ratio);
    }

    // -------------------------------------------------------------------------
    // Warning evaluation
    // The configured M1-10 range is a warning only. Ratios outside that range
    // must not be treated as rejected values.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("0.40")]
    [InlineData("0.60")]
    public void Evaluate_AtWarningBoundary_ReturnsOk(string value)
    {
        var result = _calculator.Evaluate(decimal.Parse(value));

        Assert.IsType<LimitResult.Ok>(result);
    }

    [Theory]
    [InlineData("0.39")]
    [InlineData("0.61")]
    public void Evaluate_OutsideWarningRange_ReturnsWarn(string value)
    {
        var result = _calculator.Evaluate(decimal.Parse(value));

        Assert.IsType<LimitResult.Warn>(result);
    }

    [Fact]
    public void Evaluate_WithNullRatio_ReturnsNull()
    {
        var result = _calculator.Evaluate(null);

        Assert.Null(result);
    }

    // -------------------------------------------------------------------------
    // Test helpers
    // -------------------------------------------------------------------------

    private static Composition CreateComposition(
        decimal cementPct,
        decimal waterPct,
        decimal scmPct,
        ScmType scmType)
    {
        return new Composition
        {
            CementPct = cementPct,
            WaterPct = waterPct,
            ScmPct = scmPct,
            ScmType = scmType
        };
    }

    private sealed class FakeSpecificGravityProvider : ISpecificGravityProvider
    {
        public decimal Get(Material material)
        {
            return material switch
            {
                Material.Cement => 3.15m,
                Material.Water => 1.00m,
                Material.FlyAsh => 2.35m,
                Material.Slag => 2.90m,
                Material.SilicaFume => 2.35m,
                _ => throw new ArgumentOutOfRangeException(nameof(material))
            };
        }
    }

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
