using Vulcan.Application.Interfaces;

namespace Vulcan.Application.Tests;

public sealed class TestClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; } = utcNow;
}
