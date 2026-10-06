namespace Vulcan.Application.Interfaces;

public interface IClock
{
    DateTime UtcNow { get; }
}
