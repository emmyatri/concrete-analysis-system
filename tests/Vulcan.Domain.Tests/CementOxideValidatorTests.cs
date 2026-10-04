using System.Globalization;
using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Interfaces;
using Vulcan.Domain.Services;
using Vulcan.Domain.Validation;
using static Vulcan.Infrastructure.SetupUtils;

namespace Vulcan.Domain.Tests;

public sealed class CementOxideValidatorTests
{
    private readonly TestLimitProvider _provider = new([.. LoadValidationLimits()]);

    public static TheoryData<string, decimal, decimal> NormalRanges => new()
    {
        { LimitKeys.AluminaPct, 3.0m, 7.5m },
        { LimitKeys.IronOxidePct, 0.5m, 6.0m },
        { LimitKeys.MagnesiumOxidePct, 0.1m, 4.0m },
        { LimitKeys.SulfurTrioxidePct, 1.3m, 3.0m },
    };

    public static TheoryData<string, decimal> RejectionMaximums => new()
    {
        { LimitKeys.AluminaPct, 100m },
        { LimitKeys.IronOxidePct, 100m },
        { LimitKeys.MagnesiumOxidePct, 6.0m },
        { LimitKeys.SulfurTrioxidePct, 4.5m },
    };

    public static TheoryData<string> ChemistryKeys => new()
    {
        LimitKeys.AluminaPct,
        LimitKeys.IronOxidePct,
        LimitKeys.MagnesiumOxidePct,
        LimitKeys.SulfurTrioxidePct,
    };

    [Theory]
    [MemberData(nameof(NormalRanges))]
    public void Normal_range_boundaries_are_inclusive(
        string key, decimal normalMin, decimal normalMax)
    {
        Check(key, normalMin).ShouldBeOfType<LimitResult.Ok>();
        Check(key, normalMin + 0.01m).ShouldBeOfType<LimitResult.Ok>();
        Check(key, normalMax - 0.01m).ShouldBeOfType<LimitResult.Ok>();
        Check(key, normalMax).ShouldBeOfType<LimitResult.Ok>();
        Check(key, normalMin - 0.01m).ShouldBeOfType<LimitResult.Warn>();
        Check(key, normalMax + 0.01m).ShouldBeOfType<LimitResult.Warn>();
    }

    [Theory]
    [MemberData(nameof(RejectionMaximums))]
    public void Rejection_boundaries_are_inclusive_and_override_warnings(
        string key, decimal rejectMax)
    {
        Check(key, -0.01m).ShouldBeOfType<LimitResult.Reject>();
        Check(key, 0m).ShouldBeOfType<LimitResult.Warn>();
        Check(key, 0.01m).ShouldBeOfType<LimitResult.Warn>();
        Check(key, rejectMax - 0.01m).ShouldBeOfType<LimitResult.Warn>();
        Check(key, rejectMax).ShouldBeOfType<LimitResult.Warn>();
        Check(key, rejectMax + 0.01m).ShouldBeOfType<LimitResult.Reject>();
        Check(key, 100.01m).ShouldBeOfType<LimitResult.Reject>();
    }

    [Theory]
    [MemberData(nameof(ChemistryKeys))]
    public void Missing_value_is_rejected_but_explicit_zero_is_not(string key)
    {
        var rejection = Check(key, null).ShouldBeOfType<LimitResult.Reject>();

        rejection.Message.ShouldContain("required");
        Check(key, 0m).ShouldBeOfType<LimitResult.Warn>();
    }

    [Theory]
    [MemberData(nameof(NormalRanges))]
    public void More_than_two_decimal_places_is_rejected_without_rounding(
        string key, decimal normalMin, decimal normalMax)
    {
        foreach (var value in new[] { normalMin + 0.001m, normalMax - 0.001m, 0.001m })
        {
            var rejection = Check(key, value).ShouldBeOfType<LimitResult.Reject>();

            rejection.Message.ShouldContain("two decimal places");
        }
    }

    [Theory]
    [MemberData(nameof(ChemistryKeys))]
    public void Up_to_two_decimal_places_and_insignificant_trailing_zeros_are_accepted(string key)
    {
        Check(key, 3m).ShouldNotBeOfType<LimitResult.Reject>();
        Check(key, 3.2m).ShouldNotBeOfType<LimitResult.Reject>();
        Check(key, 3.21m).ShouldNotBeOfType<LimitResult.Reject>();
        Check(key, decimal.Parse("3.200", CultureInfo.InvariantCulture))
            .ShouldNotBeOfType<LimitResult.Reject>();
    }

    [Fact]
    public void Sulfur_trioxide_at_3_point_2_is_a_warning_that_does_not_block_saving()
    {
        var results = CreateValidator().Validate(5m, 2m, 2m, 3.2m);

        results[LimitKeys.SulfurTrioxidePct].ShouldBeOfType<LimitResult.Warn>();
        results.Values.ShouldNotContain(result => result is LimitResult.Reject);
    }

    [Fact]
    public void Every_missing_field_has_its_own_rejection()
    {
        var results = CreateValidator().Validate(null, null, null, null);

        results.Count.ShouldBe(4);
        foreach (var result in results.Values)
        {
            result.ShouldBeOfType<LimitResult.Reject>();
        }
    }

    [Fact]
    public void Validation_preserves_configured_warning_and_rejection_messages()
    {
        var warning = Check(LimitKeys.SulfurTrioxidePct, 3.2m)
            .ShouldBeOfType<LimitResult.Warn>();
        var rejection = Check(LimitKeys.SulfurTrioxidePct, 4.51m)
            .ShouldBeOfType<LimitResult.Reject>();

        warning.Message.ShouldBe(_provider.Get(LimitKeys.SulfurTrioxidePct).Warn!.Message);
        rejection.Message.ShouldBe(_provider.Get(LimitKeys.SulfurTrioxidePct).Reject!.Message);
    }

    [Fact]
    public void Ranges_come_from_the_provider_rather_than_hard_coded_values()
    {
        var limits = LoadValidationLimits();
        foreach (var limit in limits.Where(limit => limit.Key == LimitKeys.SulfurTrioxidePct))
        {
            limit.Min = limit.Severity == Severity.Warn ? 42m : 40m;
            limit.Max = limit.Severity == Severity.Warn ? 48m : 50m;
        }

        var validator = new CementOxideValidator(new LimitEvaluator(new TestLimitProvider([.. limits])));

        validator.Validate(5m, 2m, 2m, 45m)[LimitKeys.SulfurTrioxidePct]
            .ShouldBeOfType<LimitResult.Ok>();
        validator.Validate(5m, 2m, 2m, 50m)[LimitKeys.SulfurTrioxidePct]
            .ShouldBeOfType<LimitResult.Warn>();
        validator.Validate(5m, 2m, 2m, 50.01m)[LimitKeys.SulfurTrioxidePct]
            .ShouldBeOfType<LimitResult.Reject>();
    }

    [Fact]
    public void Chemistry_is_checked_independently_without_requesting_mix_total_rules()
    {
        var results = CreateValidator().Validate(100m, 100m, 6m, 4.5m);

        results.Values.ShouldNotContain(result => result is LimitResult.Reject);
        results.Keys.ShouldAllBe(key => key.StartsWith("Oxide.", StringComparison.Ordinal));
        _provider.RequestedKeys.ShouldNotContain(LimitKeys.TotalPct);
    }

    private CementOxideValidator CreateValidator() => new(new LimitEvaluator(_provider));

    private LimitResult Check(string key, decimal? value)
    {
        return CreateValidator().Validate(
            key == LimitKeys.AluminaPct ? value : 5m,
            key == LimitKeys.IronOxidePct ? value : 2m,
            key == LimitKeys.MagnesiumOxidePct ? value : 2m,
            key == LimitKeys.SulfurTrioxidePct ? value : 2m)[key];
    }

    private sealed class TestLimitProvider(params ValidationLimit[] limits) : ILimitProvider
    {
        public List<string> RequestedKeys { get; } = [];

        public LimitSet Get(string key)
        {
            RequestedKeys.Add(key);
            var matches = limits.Where(limit => limit.Key == key).ToList();
            if (matches.Count == 0)
            {
                throw new KeyNotFoundException(key);
            }

            return new LimitSet
            {
                Warn = matches.SingleOrDefault(limit => limit.Severity == Severity.Warn),
                Reject = matches.SingleOrDefault(limit => limit.Severity == Severity.Reject),
            };
        }
    }
}
