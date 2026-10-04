using Vulcan.Domain.Enums;
using Vulcan.Domain.Interfaces;

namespace Vulcan.Infrastructure.Services;

public class SpecificGravityProvider(VulcanDbContext db) : ISpecificGravityProvider
{
    public decimal Get(Material material)
    {
        return db.SpecificGravity
            .Where(specificGravity => specificGravity.Material == material)
            .Select(specificGravity => specificGravity.Value)
            .Single();
    }
}
