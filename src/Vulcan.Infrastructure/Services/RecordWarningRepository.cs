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

    public async Task<IReadOnlyList<RecordWarning>> GetForBatchesAsync(
        IReadOnlyCollection<Batch> batches)
    {
        var batchIds = batches.Select(b => b.Id).ToList();

        var breakIds = batches.SelectMany(b => b.Specimens)
            .Where(s => s.BreakResult != null)
            .Select(s => s.BreakResult!.Id)
            .ToList();

        var readingIds = batches.SelectMany(b => b.Specimens)
            .SelectMany(s => s.CuringReadings)
            .Select(r => r.Id)
            .ToList();

        return await dbContext.RecordWarning
            .AsNoTracking()
            .Where(w =>
                (w.EntityType == nameof(Batch) && batchIds.Contains(w.EntityId)) ||
                (w.EntityType == nameof(BreakResult) && breakIds.Contains(w.EntityId)) ||
                (w.EntityType == nameof(CuringReading) && readingIds.Contains(w.EntityId)))
            .OrderBy(w => w.CreatedAtUtc)
            .ToListAsync();
    }
}
