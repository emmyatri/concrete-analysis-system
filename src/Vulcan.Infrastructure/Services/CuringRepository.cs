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

    public async Task AddWithWarningsAsync(
        CuringReading reading,
        Func<int, IReadOnlyList<RecordWarning>> warningFactory)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync();

        try
        {
            await dbContext.CuringReading.AddAsync(reading);

            // The first save generates the database ID for the new curing reading.
            await dbContext.SaveChangesAsync();

            var warnings = warningFactory(reading.Id);

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

    public async Task SaveChangesAsync()
    {
        await dbContext.SaveChangesAsync();
    }
}
