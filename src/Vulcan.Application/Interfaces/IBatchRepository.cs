using Vulcan.Domain.Entities;

namespace Vulcan.Application.Interfaces;

public interface IBatchRepository
{
    Task AddAsync(Batch batch);
    Task<Batch?> GetByNumberAsync(string batchNumber);
    Task<bool> ExistsAsync(string batchNumber);
    
    Task<IReadOnlyList<Batch>> SearchAsync(
        string searchTerm,
        int page,
        int pageSize);
    
    Task SaveChangesAsync();
}
