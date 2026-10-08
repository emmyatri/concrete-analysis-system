using Microsoft.EntityFrameworkCore;
using Vulcan.Application.Interfaces;
using Vulcan.Domain.Entities;

namespace Vulcan.Infrastructure.Services;

public class SpecificGravityRepository(VulcanDbContext db) : ISpecificGravityRepository
{
    public async Task<List<SpecificGravity>> GetSpecificGravitiesAsync() => await db.SpecificGravity.ToListAsync();
}
