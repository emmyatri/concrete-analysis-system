using Vulcan.Application.Interfaces;

namespace Vulcan.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
