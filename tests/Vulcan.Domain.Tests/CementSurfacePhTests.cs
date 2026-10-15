using System.Globalization;
using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Interfaces;
using Vulcan.Domain.Services;
using Vulcan.Domain.Validation;
using Vulcan.Infrastructure;

namespace Vulcan.Domain.Tests;

public sealed class CementSurfacePhTests
{
    [Theory]
    [InlineData("-0.01", "Reject")]
    [InlineData("0", "Warn")]
    [InlineData("7.09", "Warn")]
    [InlineData("7.1", "Ok")]
    [InlineData("7.5", "Ok")]
    [InlineData("8.0", "Ok")]
    [InlineData("8.01", "Warn")]
    [InlineData("8.5", "Warn")]
    [InlineData("14", "Warn")]
    [InlineData("14.01", "Reject")]
    [InlineData("7.501", "Reject")]
    [InlineData("7.500", "Ok")]
    public void Values_use_precision_and_configured_ranges(string input, string expected)
    {
        var validator = CreateValidator();
        var value = decimal.Parse(input, CultureInfo.InvariantCulture);

        var result = validator.CheckCementSurfacePh(value);

        Assert.Equal(expected, result.GetType().Name);
    }

    [Fact]
    public void Missing_value_is_rejected_before_requesting_limits()
    {
        var validator = new BatchValidator(new LimitEvaluator(new TestLimitProvider()));

        var result = Assert.IsType<LimitResult.Reject>(
            validator.CheckCementSurfacePh(null));

        Assert.Equal("Cement surface pH is required.", result.Message);
    }

    [Fact]
    public void Excessive_precision_is_rejected_before_requesting_limits()
    {
        var validator = new BatchValidator(new LimitEvaluator(new TestLimitProvider()));

        var result = Assert.IsType<LimitResult.Reject>(
            validator.CheckCementSurfacePh(7.501m));

        Assert.Equal(
            "Cement surface pH must have at most two decimal places.",
            result.Message);
    }

    [Fact]
    public void Seeded_ranges_return_the_required_messages_and_source()
    {
        var validator = CreateValidator();
        var reject = Assert.IsType<LimitResult.Reject>(
            validator.CheckCementSurfacePh(-0.01m));
        var warn = Assert.IsType<LimitResult.Warn>(
            validator.CheckCementSurfacePh(8.5m));

        Assert.Equal("pH must be within the range [0-14]", reject.Message);
        Assert.Equal(
            "Cement surface pH is outside the normal range [7.1-8.0]",
            warn.Message);

        var limits = SetupUtils.LoadValidationLimits()
            .Where(limit => limit.Key == LimitKeys.CementSurfacePh)
            .ToList();

        Assert.Equal(2, limits.Count);
        Assert.All(limits, limit =>
            Assert.Equal("Client, M1 demo (Oct 9)", limit.Source));
    }

    [Fact]
    public void Changed_limits_and_messages_are_used_without_hard_coded_ranges()
    {
        var validator = new BatchValidator(new LimitEvaluator(new TestLimitProvider(
            new ValidationLimit
            {
                Key = LimitKeys.CementSurfacePh,
                Severity = Severity.Reject,
                Min = 2m,
                Max = 12m,
                Message = "Configured rejection",
            },
            new ValidationLimit
            {
                Key = LimitKeys.CementSurfacePh,
                Severity = Severity.Warn,
                Min = 6m,
                Max = 9m,
                Message = "Configured warning",
            })));

        Assert.IsType<LimitResult.Ok>(validator.CheckCementSurfacePh(8.5m));
        var warn = Assert.IsType<LimitResult.Warn>(
            validator.CheckCementSurfacePh(9.5m));
        var reject = Assert.IsType<LimitResult.Reject>(
            validator.CheckCementSurfacePh(13m));

        Assert.Equal("Configured warning", warn.Message);
        Assert.Equal("Configured rejection", reject.Message);
    }

    private static BatchValidator CreateValidator() =>
        new(new LimitEvaluator(new TestLimitProvider(
            [.. SetupUtils.LoadValidationLimits()])));

    private sealed class TestLimitProvider(params ValidationLimit[] limits) : ILimitProvider
    {
        public LimitSet Get(string key)
        {
            Assert.Equal(LimitKeys.CementSurfacePh, key);
            var matches = limits.Where(limit => limit.Key == key).ToList();
            Assert.NotEmpty(matches);

            return new LimitSet
            {
                Reject = matches.SingleOrDefault(limit => limit.Severity == Severity.Reject),
                Warn = matches.SingleOrDefault(limit => limit.Severity == Severity.Warn),
            };
        }
    }
}
