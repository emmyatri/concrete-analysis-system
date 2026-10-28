using Vulcan.Domain.Entities;

namespace Vulcan.Application.Interfaces;

public interface IPredictionRepository
{
    Task<IReadOnlyList<PredictionEntity>> GetByBatchIdAsync(int batchId);
}
