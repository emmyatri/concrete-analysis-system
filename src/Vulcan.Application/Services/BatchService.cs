using Vulcan.Application.Interfaces;
using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Services;
using Vulcan.Domain.Validation;

namespace Vulcan.Application.Services;

public sealed class BatchService(
    IBatchRepository batchRepository,
    IRecordWarningRepository recordWarningRepository,
    BatchValidator batchValidator,
    CompositionValidator compositionValidator,
    CementOxideValidator cementOxideValidator,
    WaterCementRatioCalculator waterCementRatioCalculator,
    IClock clock)
{
    public async Task<ServiceResult<Batch>> CreateAsync(Batch batch)
    {
        batch.BatchNumber =
            BatchValidator.NormalizeBatchNumber(batch.BatchNumber);

        var validation = ValidateAll(batch);
        var errors = validation.Errors.ToList();

        if (BatchValidator.IsBatchNumberValid(batch.BatchNumber)
            && await batchRepository.ExistsAsync(batch.BatchNumber))
        {
            errors.Add(new ValidationError(
                "Batch.BatchNumber",
                "Batch number already exists."));
        }

        if (errors.Count > 0)
        {
            return ServiceResult<Batch>.Invalid(errors);
        }

        await batchRepository.AddWithWarningsAsync(
            batch,
            batchId =>
            {
                batch.Id = batchId;

                return CreateWarningRecords(
                    batch,
                    validation.Warnings);
            });

        return ServiceResult<Batch>.Success(batch, validation.Warnings);
    }

    public async Task<Batch?> GetByNumberAsync(string batchNumber)
    {
        var normalizedBatchNumber =
            BatchValidator.NormalizeBatchNumber(batchNumber);

        return await batchRepository.GetByNumberAsync(normalizedBatchNumber);
    }

    public async Task<IReadOnlyList<Batch>> SearchAsync(
        string searchTerm,
        int page = 1)
    {
        const int PageSize = 50;

        if (page < 1)
        {
            page = 1;
        }

        searchTerm = BatchValidator.NormalizeBatchNumber(searchTerm);

        return await batchRepository.SearchAsync(
            searchTerm,
            page,
            PageSize);
    }

    public async Task<ServiceResult<Batch>> UpdateAsync(
        string batchNumber,
        Batch updatedBatch)
    {
        var normalizedBatchNumber =
            BatchValidator.NormalizeBatchNumber(batchNumber);

        var existingBatch =
            await batchRepository.GetByNumberAsync(normalizedBatchNumber);

        if (existingBatch is null)
        {
            return ServiceResult<Batch>.NotFound(
            [
                new ValidationError(
                    "Batch.BatchNumber",
                    "Batch was not found.")
            ]);
        }

        updatedBatch.BatchNumber =
            BatchValidator.NormalizeBatchNumber(updatedBatch.BatchNumber);

        var validation = ValidateAll(updatedBatch);
        var errors = validation.Errors.ToList();

        if (!string.Equals(
                normalizedBatchNumber,
                updatedBatch.BatchNumber,
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new ValidationError(
                "Batch.BatchNumber",
                "Batch number cannot be changed."));
        }

        if (errors.Count > 0)
        {
            return ServiceResult<Batch>.Invalid(errors);
        }

        existingBatch.CastAt = updatedBatch.CastAt;
        existingBatch.FinalWeightLb = updatedBatch.FinalWeightLb;
        existingBatch.ExpectedFinalBreakPsi =
            updatedBatch.ExpectedFinalBreakPsi;
        existingBatch.CementType = updatedBatch.CementType;
        existingBatch.MixWaterPh = updatedBatch.MixWaterPh;

        existingBatch.Composition!.CementPct =
            updatedBatch.Composition!.CementPct;
        existingBatch.Composition.ScmPct =
            updatedBatch.Composition.ScmPct;
        existingBatch.Composition.ScmType =
            updatedBatch.Composition.ScmType;
        existingBatch.Composition.CoarseAggregatePct =
            updatedBatch.Composition.CoarseAggregatePct;
        existingBatch.Composition.FineAggregatePct =
            updatedBatch.Composition.FineAggregatePct;
        existingBatch.Composition.WaterPct =
            updatedBatch.Composition.WaterPct;

        existingBatch.CementOxideAnalysis!.AluminaPct =
            updatedBatch.CementOxideAnalysis!.AluminaPct;
        existingBatch.CementOxideAnalysis.IronOxidePct =
            updatedBatch.CementOxideAnalysis.IronOxidePct;
        existingBatch.CementOxideAnalysis.MagnesiumOxidePct =
            updatedBatch.CementOxideAnalysis.MagnesiumOxidePct;
        existingBatch.CementOxideAnalysis.SulfurTrioxidePct =
            updatedBatch.CementOxideAnalysis.SulfurTrioxidePct;

        var warningRecords = CreateWarningRecords(
            existingBatch,
            validation.Warnings);

        await recordWarningRepository.AddRangeAsync(warningRecords);

        await batchRepository.SaveChangesAsync();

        return ServiceResult<Batch>.Success(
            existingBatch,
            validation.Warnings);
    }

    private ValidationOutcome ValidateAll(Batch batch)
    {
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationError>();

        ValidateBatch(batch, errors, warnings);

        if (batch.Composition is null)
        {
            errors.Add(new ValidationError(
                "Composition",
                "Mix composition is required."));
        }
        else
        {
            errors.AddRange(
                compositionValidator.Validate(batch.Composition));

            var ratio = waterCementRatioCalculator.Calculate(batch.Composition);
            var ratioResult = waterCementRatioCalculator.Evaluate(ratio);

            if (ratioResult is LimitResult.Reject reject)
            {
                errors.Add(new ValidationError(
                    LimitKeys.WaterCementRatio,
                    reject.Message));
            }
            else if (ratioResult is LimitResult.Warn warn)
            {
                warnings.Add(new ValidationError(
                    LimitKeys.WaterCementRatio,
                    warn.Message));
            }
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
            warnings.AddRange(oxideResult.Warnings);
        }

        return new ValidationOutcome(errors, warnings);
    }

    private void ValidateBatch(
        Batch batch,
        List<ValidationError> errors,
        List<ValidationError> warnings)
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

        if (!batchValidator.IsExpectedFinalBreakPsiValid(
                batch.ExpectedFinalBreakPsi))
        {
            errors.Add(new ValidationError(
                LimitKeys.ExpectedFinalBreakPsi,
                "Expected final break PSI is outside the allowed range."));
        }

        var phResult = batchValidator.CheckMixWaterPh(batch.MixWaterPh);

        if (phResult is LimitResult.Reject reject)
        {
            errors.Add(new ValidationError(
                LimitKeys.MixWaterPh,
                reject.Message));
        }
        else if (phResult is LimitResult.Warn warn)
        {
            warnings.Add(new ValidationError(
                LimitKeys.MixWaterPh,
                warn.Message));
        }
    }

    private IReadOnlyList<RecordWarning> CreateWarningRecords(
        Batch batch,
        IReadOnlyList<ValidationError> warnings)
    {
        var ratio = batch.Composition is null
            ? null
            : waterCementRatioCalculator.Calculate(batch.Composition);

        return warnings.Select(warning => new RecordWarning
        {
            EntityType = nameof(Batch),
            EntityId = batch.Id,
            FieldName = warning.Key,
            LimitKey = warning.Key,
            Value = warning.Key switch
            {
                LimitKeys.MixWaterPh => batch.MixWaterPh,
                LimitKeys.WaterCementRatio => ratio ?? 0m,
                LimitKeys.AluminaPct => batch.CementOxideAnalysis!.AluminaPct,
                LimitKeys.IronOxidePct => batch.CementOxideAnalysis!.IronOxidePct,
                LimitKeys.MagnesiumOxidePct => batch.CementOxideAnalysis!.MagnesiumOxidePct,
                LimitKeys.SulfurTrioxidePct => batch.CementOxideAnalysis!.SulfurTrioxidePct,
                _ => throw new InvalidOperationException(
                    $"Unsupported batch warning key '{warning.Key}'.")
            },
            Message = warning.Message
        }).ToList();
    }
}
