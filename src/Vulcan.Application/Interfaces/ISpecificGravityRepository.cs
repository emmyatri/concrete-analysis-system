using Vulcan.Domain.Entities;

namespace Vulcan.Application.Interfaces;

public interface ISpecificGravityRepository
{
    public Task<List<SpecificGravity>> GetSpecificGravitiesAsync();
}
