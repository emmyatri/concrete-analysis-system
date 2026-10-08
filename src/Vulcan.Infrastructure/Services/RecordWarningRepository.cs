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
}
