using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Interfaces;
using Vulcan.Domain.Services;
using Vulcan.Domain.Validation;
using static Vulcan.Infrastructure.SetupUtils;

namespace Vulcan.Domain.Tests.Units;

public class BreakResultValidatorTests
{
    private const string _brokenAtKey = "Break.BrokenAt";
    private const string _specimenIdKey = "Break.SpecimenId";

    private readonly BreakResultValidator _validator =
        new(new LimitEvaluator(
            new FakeLimitProvider([.. LoadValidationLimits()])));

    private static readonly DateTime _castAt =
        new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime _now =
        new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0)]
    [InlineData(10001)]
    public void Validate_StrengthOutsideRejectRange_ReturnsError(int strengthPsi)
    {
        var breakResult = CreateBreakResult(strengthPsi);

        var result = _validator.Validate(
            breakResult,
            _castAt,
            false,
            _now);

        Assert.Contains(
            result.Errors,
            error => error.Key == LimitKeys.StrengthPsi);

        Assert.DoesNotContain(
            result.Warnings,
            warning => warning.Key == LimitKeys.StrengthPsi);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2999)]
    public void Validate_LowAcceptedStrength_ReturnsWarning(int strengthPsi)
    {
        var breakResult = CreateBreakResult(strengthPsi);

        var result = _validator.Validate(
            breakResult,
            _castAt,
            false,
            _now);

        Assert.DoesNotContain(
            result.Errors,
            error => error.Key == LimitKeys.StrengthPsi);

        Assert.Contains(
            result.Warnings,
            warning => warning.Key == LimitKeys.StrengthPsi);
    }

    [Theory]
    [InlineData(3000)]
    [InlineData(10000)]
    public void Validate_StrengthWithinNormalRange_IsOk(int strengthPsi)
    {
        var breakResult = CreateBreakResult(strengthPsi);

        var result = _validator.Validate(
            breakResult,
            _castAt,
            false,
            _now);

        Assert.DoesNotContain(
            result.Errors,
            error => error.Key == LimitKeys.StrengthPsi);

        Assert.DoesNotContain(
            result.Warnings,
            warning => warning.Key == LimitKeys.StrengthPsi);
    }

    [Fact]
    public void Validate_WhenSpecimenAlreadyHasResult_ReturnsSpecimenError()
    {
        var breakResult = CreateBreakResult(3000);

        var result = _validator.Validate(
            breakResult,
            _castAt,
            true,
            _now);

        Assert.Contains(
            result.Errors,
            error => error.Key == _specimenIdKey);
    }

    [Fact]
    public void Validate_WhenBrokenBeforeCastTime_ReturnsBrokenAtError()
    {
        var breakResult = CreateBreakResult(
            3000,
            _castAt.AddMinutes(-1));

        var result = _validator.Validate(
            breakResult,
            _castAt,
            false,
            _now);

        Assert.Contains(
            result.Errors,
            error => error.Key == _brokenAtKey);
    }
    [Fact]
    public void Validate_WhenBrokenAtCastTime_IsValid()
    {
        var breakResult = CreateBreakResult(
            3000,
            _castAt);

        var result = _validator.Validate(
            breakResult,
            _castAt,
            false,
            _now);

        Assert.DoesNotContain(
            result.Errors,
            error => error.Key == _brokenAtKey);
    }

    [Fact]
    public void Validate_WhenBrokenInFuture_ReturnsBrokenAtError()
    {
        var breakResult = CreateBreakResult(
            3000,
            _now.AddMinutes(1));

        var result = _validator.Validate(
            breakResult,
            _castAt,
            false,
            _now);

        Assert.Contains(
            result.Errors,
            error => error.Key == _brokenAtKey);
    }

    [Fact]
    public void Validate_WhenBrokenAtCurrentTime_IsValid()
    {
        var breakResult = CreateBreakResult(
            3000,
            _now);

        var result = _validator.Validate(
            breakResult,
            _castAt,
            false,
            _now);

        Assert.DoesNotContain(
            result.Errors,
            error => error.Key == _brokenAtKey);
    }

    [Fact]
    public void Validate_WithValidBreakResult_ReturnsNoErrorsOrWarnings()
    {
        var breakResult = CreateBreakResult(3000);

        var result = _validator.Validate(
            breakResult,
            _castAt,
            false,
            _now);

        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    private static BreakResult CreateBreakResult(
        int strengthPsi,
        DateTime? brokenAt = null)
    {
        return new BreakResult
        {
            SpecimenId = 1,
            StrengthPsi = strengthPsi,
            BrokenAt = brokenAt ?? _castAt.AddDays(1)
        };
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

    [Theory]
    [InlineData(TestAgeDays.Day3)]
    [InlineData(TestAgeDays.Day7)]
    public void Validate_LowEarlyBreak_IsNotFlagged(TestAgeDays testAgeDays)
    {
        // The 3,000 PSI warning applies to 28-day breaks only (client-decisions §3).
        var result = _validator.Validate(
            CreateBreakResult(2500),
            _castAt,
            false,
            _now,
            testAgeDays);

        Assert.Empty(result.Errors);
        Assert.DoesNotContain(
            result.Warnings,
            warning => warning.Key == LimitKeys.StrengthPsi);
    }

    [Fact]
    public void Validate_EarlyBreakOutsideRejectRange_StillReturnsError()
    {
        var result = _validator.Validate(
            CreateBreakResult(0),
            _castAt,
            false,
            _now,
            TestAgeDays.Day3);

        Assert.Contains(
            result.Errors,
            error => error.Key == LimitKeys.StrengthPsi);
    }
}
