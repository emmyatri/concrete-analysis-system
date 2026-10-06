using Vulcan.Domain.Entities;

namespace Vulcan.Application.Interfaces;

public interface ISpecimenRepository
{
    Task<Batch?> GetBatchAsync(int batchId);

    Task<Specimen?> GetByIdAsync(int specimenId);

    Task<bool> LabelExistsAsync(
        int batchId,
        string label);

    Task AddAsync(Specimen specimen);

    Task AddBreakResultAsync(BreakResult breakResult);

    Task AddBreakResultWithWarningsAsync(
        BreakResult breakResult,
        Func<int, IReadOnlyList<RecordWarning>> warningFactory);

    Task SaveChangesAsync();
}
