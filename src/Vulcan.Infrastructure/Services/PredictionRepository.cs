using Vulcan.Application.Interfaces;
using Vulcan.Domain.Entities;

namespace Vulcan.Infrastructure.Services;

public class PredictionRepository(VulcanDbContext db) : IPredictionRepository
{
    public List<PredictionEntity> GetPredictions(int batchId)
    {
        return [..db.Prediction
            .Where(p => p.BatchId == batchId)];
    }
}
