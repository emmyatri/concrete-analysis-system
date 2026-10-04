using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Interfaces;
using Vulcan.Domain.Services;
using Vulcan.Domain.Validation;
using static Vulcan.Infrastructure.SetupUtils;

namespace Vulcan.Domain.Tests;

/// <summary>Mix water pH (FR-7): Reject outside 1–9 with the range in the message, Warn outside 7.0–7.5.</summary>
public class MixWaterPhTests
{
    private readonly BatchValidator _validator = new(
        new LimitEvaluator(new FakeLimitProvider([.. LoadValidationLimits()])));

    [Theory]
    [InlineData("0.9", "Reject")]
    [InlineData("1", "Warn")]
    [InlineData("6.9", "Warn")]
    [InlineData("7", "Ok")]
    [InlineData("7.5", "Ok")]
    [InlineData("7.6", "Warn")]
    [InlineData("9", "Warn")]
    [InlineData("9.1", "Reject")]
    public void MixWaterPh_IsCheckedAgainstBothRanges(string value, string expected)
    {
        var result = _validator.CheckMixWaterPh(decimal.Parse(value));

        Assert.Equal(expected, result.GetType().Name);
    }

    [Fact]
    public void MixWaterPh_Rejected_MessageStatesTheRange()
    {
        var result = Assert.IsType<LimitResult.Reject>(_validator.CheckMixWaterPh(9.5m));

        Assert.Contains("[1-9]", result.Message);
    }

    // Supplies the real M1-10 seed limits to LimitEvaluator without a database.
    private sealed class FakeLimitProvider(params ValidationLimit[] limits)
        : ILimitProvider
    {
        public LimitSet Get(string key)
        {
            var matches = limits.Where(limit => limit.Key == key).ToList();

            if (matches.Count == 0)
            {
                throw new KeyNotFoundException(key);
            }

            return new LimitSet
            {
                Warn = matches.FirstOrDefault(limit => limit.Severity == Severity.Warn),
                Reject = matches.FirstOrDefault(limit => limit.Severity == Severity.Reject),
            };
        }
    }
}
