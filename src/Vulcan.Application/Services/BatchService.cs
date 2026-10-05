using Vulcan.Application.Interfaces;
using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Services;
using Vulcan.Domain.Validation;

namespace Vulcan.Application.Services;

public sealed class BatchService(
    IBatchRepository batchRepository,
    BatchValidator batchValidator,
    CompositionValidator compositionValidator,
    CementOxideValidator cementOxideValidator,
    IClock clock)
{
    public async Task<ServiceResult<Batch>> CreateAsync(Batch batch)
    {
        var errors = new List<ValidationError>();

        batch.BatchNumber = BatchValidator.NormalizeBatchNumber(batch.BatchNumber);

        ValidateBatch(batch, errors);

        if (batch.Composition is null)
        {
            errors.Add(new ValidationError(
                "Composition",
                "Mix composition is required."));
        }
        else
        {
            errors.AddRange(compositionValidator.Validate(batch.Composition));
        }

        if (batch.CementOxideAnalysis is null)
        {
            errors.Add(new ValidationError(
                "CementOxideAnalysis",
                "Cement oxide analysis is required."));
        }
        else
        {
            var oxideResult = cementOxideValidator.Validate(
                batch.CementOxideAnalysis.AluminaPct,
                batch.CementOxideAnalysis.IronOxidePct,
                batch.CementOxideAnalysis.MagnesiumOxidePct,
                batch.CementOxideAnalysis.SulfurTrioxidePct);

            errors.AddRange(oxideResult.Errors);
        }

        if (BatchValidator.IsBatchNumberValid(batch.BatchNumber)
            && await batchRepository.ExistsAsync(batch.BatchNumber))
        {
            errors.Add(new ValidationError(
                "Batch.BatchNumber",
                "Batch number already exists."));
        }

        if (errors.Count > 0)
        {
            return ServiceResult<Batch>.Failure(errors);
        }

        await batchRepository.AddAsync(batch);
        await batchRepository.SaveChangesAsync();

        return ServiceResult<Batch>.Success(batch);
    }

    private void ValidateBatch(
        Batch batch,
        List<ValidationError> errors)
    {
        if (!BatchValidator.IsBatchNumberValid(batch.BatchNumber))
        {
            errors.Add(new ValidationError(
                "Batch.BatchNumber",
                "Batch number is required and may contain only letters, numbers, and dashes."));
        }

        if (!batchValidator.IsFinalWeightValid(batch.FinalWeightLb))
        {
            errors.Add(new ValidationError(
                LimitKeys.FinalWeightLb,
                "Final weight is invalid."));
        }

        if (!BatchValidator.IsCastAtValid(batch.CastAt, clock.UtcNow))
        {
            errors.Add(new ValidationError(
                "Batch.CastAt",
                "Pour date and time are required and cannot be in the future."));
        }

        if (!BatchValidator.IsCementTypeValid(batch.CementType))
        {
            errors.Add(new ValidationError(
                "Batch.CementType",
                "Cement type must be Type I, II, or III."));
        }

        if (!batchValidator.IsExpectedFinalBreakPsiValid(batch.ExpectedFinalBreakPsi))
        {
            errors.Add(new ValidationError(
                LimitKeys.ExpectedFinalBreakPsi,
                "Expected final break PSI is outside the allowed range."));
        }

        if (batchValidator.CheckMixWaterPh(batch.MixWaterPh) is LimitResult.Reject reject)
        {
            errors.Add(new ValidationError(
                LimitKeys.MixWaterPh,
                reject.Message));
        }
    }
}
