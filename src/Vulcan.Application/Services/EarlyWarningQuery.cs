using Vulcan.Application.Contracts;
using Vulcan.Application.Interfaces;

namespace Vulcan.Application.Services;

public sealed class EarlyWarningQuery(IBatchRepository batchRepository, IEarlyWarningEvaluator evaluator)
{
    public async Task<IReadOnlyList<EarlyWarningBatchDTO>> GetAsync(
        bool includeSynthetic,
        CancellationToken cancellationToken)
    {
        var batches = await batchRepository.GetEarlyWarningCandidatesAsync(includeSynthetic, cancellationToken);
        var results = new List<EarlyWarningBatchDTO>();

        foreach (var batch in batches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var evaluation = await evaluator.EvaluateAsync(batch, cancellationToken);
            if (evaluation.EarlyWarnings.Count == 0)
            {
                continue;
            }

            results.Add(new EarlyWarningBatchDTO(
                batch.Id,
                batch.BatchNumber,
                batch.IsSynthetic,
                evaluation.AtRisk,
                evaluation.Projected28DayPsi,
                evaluation.ShortfallPsi,
                evaluation.ShortfallPct,
                evaluation.EarlyWarnings));
        }

        return results
            .OrderByDescending(result => result.ShortfallPct)
            .ThenByDescending(result => result.EarlyWarnings
                .Where(warning => !warning.LimitKey.StartsWith("Prediction.", StringComparison.Ordinal))
                .Select(warning => warning.LimitKey)
                .Distinct(StringComparer.Ordinal)
                .Count())
            .ThenBy(result => result.BatchNumber, StringComparer.Ordinal)
            .ToList();
    }
}
