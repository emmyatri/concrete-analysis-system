using Vulcan.Domain.Entities;

namespace Vulcan.Application.Interfaces;

public interface IRecordWarningRepository
{
    Task AddRangeAsync(IEnumerable<RecordWarning> warnings);

    /// <summary>
    /// Removes the warnings saved on a batch's own fields (batch, recipe and oxides), so an edit
    /// can save the current ones. Cylinder, reading and break warnings are kept.
    /// </summary>
    Task RemoveBatchFieldWarningsAsync(int batchId);

    Task<IReadOnlyList<RecordWarning>> GetForBatchesAsync(
        IReadOnlyCollection<Batch> batches);
}
