using Microsoft.EntityFrameworkCore;
using Vulcan.Application.Interfaces;
using Vulcan.Domain.Entities;

namespace Vulcan.Infrastructure.Services;

public sealed class CuringRepository(VulcanDbContext dbContext)
    : ICuringRepository
{
    public async Task<Specimen?> GetSpecimenAsync(int specimenId)
    {
        return await dbContext.Specimen
            .Include(x => x.Batch)
            .SingleOrDefaultAsync(x => x.Id == specimenId);
    }

    public async Task AddAsync(CuringReading reading)
    {
        await dbContext.CuringReading.AddAsync(reading);
    }

    public async Task SaveChangesAsync()
    {
        await dbContext.SaveChangesAsync();
    }
}
