using Vulcan.Domain.Entities;

namespace Vulcan.Application.Interfaces;

public interface IRecordWarningRepository
{
    Task AddRangeAsync(IEnumerable<RecordWarning> warnings);

    Task<IReadOnlyDictionary<int, int>> GetBatchWarningCountsAsync(
        IReadOnlyCollection<int> batchIds);
}
