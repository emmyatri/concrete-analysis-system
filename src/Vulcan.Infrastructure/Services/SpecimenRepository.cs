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

    public async Task AddBreakResultWithWarningsAsync(
        BreakResult breakResult,
        Func<int, IReadOnlyList<RecordWarning>> warningFactory)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync();

        try
        {
            await dbContext.BreakResult.AddAsync(breakResult);

            // The first save generates the database ID for the break result.
            await dbContext.SaveChangesAsync();

            var warnings = warningFactory(breakResult.Id);

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

    public async Task<Batch?> GetBatchByNumberAsync(string batchNumber)
    {
        return await dbContext.Batch
            .SingleOrDefaultAsync(x => x.BatchNumber == batchNumber);
    }

    public async Task<IReadOnlyList<Specimen>> GetPendingBreaksAsync(bool includeSynthetic)
    {
        return await dbContext.Specimen
            .Include(x => x.Batch)
            .Where(x => x.BreakResult == null)
            .Where(x => includeSynthetic || !x.IsSynthetic)
            .ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await dbContext.SaveChangesAsync();
    }
}
