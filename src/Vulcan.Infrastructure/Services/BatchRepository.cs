using Microsoft.EntityFrameworkCore;
using Vulcan.Application.Interfaces;
using Vulcan.Domain.Entities;

namespace Vulcan.Infrastructure.Services;

public sealed class BatchRepository(VulcanDbContext dbContext) : IBatchRepository
{
    public async Task AddAsync(Batch batch)
    {
        await dbContext.Batch.AddAsync(batch);
    }

    public async Task<Batch?> GetByNumberAsync(string batchNumber)
    {
        return await dbContext.Batch
            .Include(x => x.Composition)
            .Include(x => x.CementOxideAnalysis)
            .Include(x => x.Specimens)
            .ThenInclude(x => x.BreakResult)
            .Include(x => x.Specimens)
            .ThenInclude(x => x.CuringReadings)
            .SingleOrDefaultAsync(x => x.BatchNumber == batchNumber);
    }

    public async Task<bool> ExistsAsync(string batchNumber)
    {
        return await dbContext.Batch
            .AnyAsync(x => x.BatchNumber == batchNumber);
    }

    public async Task SaveChangesAsync()
    {
        await dbContext.SaveChangesAsync();
    }
}
