using Microsoft.EntityFrameworkCore;
using Vulcan.Application.Interfaces;
using Vulcan.Domain.Entities;

namespace Vulcan.Infrastructure.Services;

public sealed class PredictionRepository(VulcanDbContext dbContext)
    : IPredictionRepository
{
    public async Task<IReadOnlyList<PredictionEntity>> GetByBatchIdAsync(int batchId)
    {
        return await dbContext.Prediction
            .AsNoTracking()
            .Where(p => p.BatchId == batchId)
            .OrderBy(p => p.HorizonDays)
            .ToListAsync();
    }
}
