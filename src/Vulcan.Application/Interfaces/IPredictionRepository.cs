using Vulcan.Domain.Entities;

namespace Vulcan.Application.Interfaces;

public interface IPredictionRepository
{
    public List<PredictionEntity> GetPredictions(int batchId);
}
