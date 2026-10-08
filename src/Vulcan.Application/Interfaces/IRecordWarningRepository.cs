using Vulcan.Domain.Entities;

namespace Vulcan.Application.Interfaces;

public interface IRecordWarningRepository
{
    Task AddRangeAsync(IEnumerable<RecordWarning> warnings);

    Task<IReadOnlyList<RecordWarning>> GetForBatchesAsync(
        IReadOnlyCollection<Batch> batches);
}
