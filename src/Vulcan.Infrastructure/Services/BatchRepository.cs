using Microsoft.EntityFrameworkCore;
using Vulcan.Application.Interfaces;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;

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
        string search,
        DateTime? castFrom,
        DateTime? castTo,
        CementType? cementType,
        bool? hasWarnings,
        bool? includeSynthetic,
        int page
    )
    {
        bool includeSyntheticResults = includeSynthetic ??= true;
        var escapedSearchTerm = EscapeLikePattern(search);

        var query = dbContext.Batch
            .AsNoTracking()
            .AsSplitQuery()
            .Include(b => b.Composition)
            .Include(b => b.Specimens)
            .ThenInclude(s => s.BreakResult)
            .Include(b => b.Specimens)
            .ThenInclude(s => s.CuringReadings)
            .Where(x => EF.Functions.Like(
                x.BatchNumber,
                $"%{escapedSearchTerm}%",
                @"\"));

        // assuming inclusive ranges
        if (castFrom.HasValue)
        {
            query = query.Where(b => b.CastAt >= castFrom);
        }

        if (castTo.HasValue)
        {
            query = query.Where(b => b.CastAt <= castTo);
        }

        if (cementType.HasValue)
        {
            query = query.Where(b => b.CementType == cementType);
        }

        if (hasWarnings.HasValue)
        {
            query = query.Where(b =>
                dbContext.RecordWarning.Any(w =>
                    w.EntityType == "Batch" &&
                    w.EntityId == b.Id
                ) == hasWarnings);
        }

        if (!includeSyntheticResults)
        {
            query = query.Where(b => b.IsSynthetic == false);
        }

        return await query
            .OrderByDescending(b => b.CastAt)
            .ThenByDescending(b => b.Id)
            .Skip((page - 1) * 50)
            .Take(50)
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
