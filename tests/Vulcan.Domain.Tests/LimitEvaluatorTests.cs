using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Interfaces;
using Vulcan.Domain.Services;
using Vulcan.Domain.Validation;
using static Vulcan.Infrastructure.SetupUtils;

namespace Vulcan.Domain.Tests;

public sealed class LimitEvaluatorTests
{
    private sealed class FakeLimitProvider(params ValidationLimit[] limits) : ILimitProvider
    {
        public LimitSet Get(string key)
        {
            var matches = limits.Where(l => l.Key == key).ToList();
            if (matches.Count == 0)
            {
                throw new KeyNotFoundException(key);
            }

            return new LimitSet
            {
                Warn = matches.FirstOrDefault(l => l.Severity == Severity.Warn),
                Reject = matches.FirstOrDefault(l => l.Severity == Severity.Reject),
            };
        }
    }

    [Fact]
    public void Limit_evaluator_handles_ranges_properly()
    {
        ValidationLimit[] validationLimits = [
            new()
            {
                Key = "Primary",
                Severity = Severity.Warn,
                Min = 30,
                Message = "Don't do that"
            },
            new()
            {
                Key = "Primary",
                Severity = Severity.Reject,
                Min = 0,
                Max = 100,
                Message = "Really, don't"
            }
        ];
        var limitEvaluator = new LimitEvaluator(
            new FakeLimitProvider(validationLimits));

        limitEvaluator
            .Check("Primary", 30)
            .ShouldBeOfType<LimitResult.Ok>();
        limitEvaluator
            .Check("Primary", 29.99)
            .ShouldBeOfType<LimitResult.Warn>()
            .Message
            .ShouldBe("Don't do that");
        limitEvaluator
            .Check("Primary", -0.01)
            .ShouldBeOfType<LimitResult.Reject>()
            .Message
            .ShouldBe("Really, don't");
    }

    [Fact]
    public void Every_seed_limit_is_inclusive_at_its_bounds()
    {
        var evaluator = new LimitEvaluator(
            new FakeLimitProvider([.. LoadValidationLimits()]));

        foreach (var limit in LoadValidationLimits())
        {
            if (limit.Min is { } min)
            {
                evaluator.Check(limit.Key, min).ShouldNotBeOfType<LimitResult.Reject>(limit.Key);
                evaluator.Check(limit.Key, min - 0.001m).ShouldNotBeOfType<LimitResult.Ok>(limit.Key);
            }

            if (limit.Max is { } max)
            {
                evaluator.Check(limit.Key, max).ShouldNotBeOfType<LimitResult.Reject>(limit.Key);
                evaluator.Check(limit.Key, max + 0.001m).ShouldNotBeOfType<LimitResult.Ok>(limit.Key);
            }

            limit.Message.ShouldNotBeNullOrWhiteSpace(limit.Key);
        }

        evaluator.Check(LimitKeys.SulfurTrioxidePct, 4.0m).ShouldBeOfType<LimitResult.Warn>();
        evaluator.Check(LimitKeys.SulfurTrioxidePct, 4.6m).ShouldBeOfType<LimitResult.Reject>();
    }
}
