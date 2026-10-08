using Microsoft.EntityFrameworkCore;
using Vulcan.Application.Interfaces;
using Vulcan.Domain.Entities;

namespace Vulcan.Infrastructure.Services;

public sealed class RecordWarningRepository(VulcanDbContext dbContext)
    : IRecordWarningRepository
{
    public async Task AddRangeAsync(IEnumerable<RecordWarning> warnings)
    {
        await dbContext.RecordWarning.AddRangeAsync(warnings);
    }

    // Retrieves persisted warning counts for multiple batches in one query,
    // avoiding a separate database lookup for each batch in the list.
    public async Task<IReadOnlyDictionary<int, int>> GetBatchWarningCountsAsync(
        IReadOnlyCollection<int> batchIds)
    {
        if (batchIds.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        return await dbContext.RecordWarning
            .AsNoTracking()
            .Where(w => w.EntityType == nameof(Batch) &&
                        batchIds.Contains(w.EntityId))
            .GroupBy(w => w.EntityId)
            .Select(g => new { BatchId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BatchId, x => x.Count);
    }
}
