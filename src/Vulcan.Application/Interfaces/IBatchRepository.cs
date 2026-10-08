using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;

namespace Vulcan.Application.Interfaces;

public interface IBatchRepository
{
    Task AddAsync(Batch batch);
    Task AddWithWarningsAsync(
        Batch batch,
        Func<int, IReadOnlyList<RecordWarning>> warningFactory);
    Task<Batch?> GetByNumberAsync(string batchNumber);
    Task<bool> ExistsAsync(string batchNumber);

    Task<IReadOnlyList<Batch>> SearchAsync(
        string search,
        DateTime? castFrom,
        DateTime? castTo,
        CementType? cementType,
        bool? hasWarnings,
        bool? includeSynthetic,
        int page);

    Task SaveChangesAsync();
}
