using Vulcan.Domain.Enums;

namespace Vulcan.Domain.Interfaces;

public interface ISpecificGravityProvider
{
    decimal Get(Material material);
}
