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

    public async Task AddWithWarningsAsync(
        Batch batch,
        Func<int, IReadOnlyList<RecordWarning>> warningFactory)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync();

        try
        {
            await dbContext.Batch.AddAsync(batch);

            // The first save generates the database ID for the new batch.
            await dbContext.SaveChangesAsync();

            var warnings = warningFactory(batch.Id);

            if (warnings.Count > 0)
            {
                await dbContext.RecordWarning.AddRangeAsync(warnings);
                await dbContext.SaveChangesAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
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

    public async Task<IReadOnlyList<Batch>> SearchAsync(
        string searchTerm,
        int page,
        int pageSize)
    {
        var escapedSearchTerm = EscapeLikePattern(searchTerm);

        return await dbContext.Batch
            .Where(x => EF.Functions.Like(
                x.BatchNumber,
                $"%{escapedSearchTerm}%",
                @"\"))
            .OrderByDescending(x => x.CastAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await dbContext.SaveChangesAsync();
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("_", @"\_");
    }
}
