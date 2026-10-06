using Vulcan.Domain.Interfaces;

namespace Vulcan.Infrastructure.Services;

public sealed class SystemTechnician : ICurrentTechnician
{
    public string Name => "System";
}
