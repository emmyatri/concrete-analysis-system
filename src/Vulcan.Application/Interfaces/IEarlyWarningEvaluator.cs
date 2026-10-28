using Vulcan.Application.Contracts;
using Vulcan.Domain.Entities;

namespace Vulcan.Application.Interfaces;

public interface IEarlyWarningEvaluator
{
    Task<EarlyWarningEvaluation> EvaluateAsync(Batch batch, CancellationToken cancellationToken);
}
