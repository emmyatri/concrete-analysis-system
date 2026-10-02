using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Interfaces;

namespace Vulcan.Infrastructure.Services;

public class LimitProvider(VulcanDbContext dbContext) : ILimitProvider
{
    public LimitSet Get(string key)
    {
        var limits = dbContext
            .ValidationLimit
            .Where(v => v.Key == key)
            .ToList();

        if (limits.Count == 0)
        {
            throw new InvalidOperationException(
                $"No validation limits configured for key '{key}'.");
        }

        var warningLimit = limits.FirstOrDefault(x => x.Severity == Severity.Warn);
        var rejectLimit = limits.FirstOrDefault(x => x.Severity == Severity.Reject);

        return new LimitSet
        {
            Warn = warningLimit,
            Reject = rejectLimit
        };
    }
}
