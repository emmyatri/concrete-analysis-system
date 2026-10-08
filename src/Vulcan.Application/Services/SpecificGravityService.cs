using Vulcan.Application.Interfaces;
using Vulcan.Domain.Entities;

namespace Vulcan.Application.Services;

public class SpecificGravityService(ISpecificGravityRepository specificGravityRepository)
{
    public async Task<List<SpecificGravity>> GetSpecificGravitiesAsync() => await specificGravityRepository.GetSpecificGravitiesAsync();
}
