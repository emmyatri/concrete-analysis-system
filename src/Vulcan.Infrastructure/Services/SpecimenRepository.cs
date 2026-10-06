using Microsoft.EntityFrameworkCore;
using Vulcan.Application.Interfaces;
using Vulcan.Domain.Entities;

namespace Vulcan.Infrastructure.Services;

public sealed class SpecimenRepository(VulcanDbContext dbContext) : ISpecimenRepository
{
    public async Task<Batch?> GetBatchAsync(int batchId)
    {
        return await dbContext.Batch
            .SingleOrDefaultAsync(x => x.Id == batchId);
    }

    public async Task<bool> LabelExistsAsync(
        int batchId,
        string label)
    {
        return await dbContext.Specimen
            .AnyAsync(x =>
                x.BatchId == batchId &&
                x.Label == label);
    }

    public async Task AddAsync(Specimen specimen)
    {
        await dbContext.Specimen.AddAsync(specimen);
    }

    public async Task<Specimen?> GetByIdAsync(int specimenId)
    {
        return await dbContext.Specimen
            .Include(x => x.Batch)
            .Include(x => x.BreakResult)
            .SingleOrDefaultAsync(x => x.Id == specimenId);
    }

    public async Task AddBreakResultAsync(BreakResult breakResult)
    {
        await dbContext.BreakResult.AddAsync(breakResult);
    }

    public async Task SaveChangesAsync()
    {
        await dbContext.SaveChangesAsync();
    }
}
