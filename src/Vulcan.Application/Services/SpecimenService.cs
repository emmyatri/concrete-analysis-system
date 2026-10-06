using Vulcan.Application.Interfaces;
using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Services;
using Vulcan.Domain.Validation;

namespace Vulcan.Application.Services;

public sealed class SpecimenService(
    ISpecimenRepository specimenRepository,
    SpecimenValidator specimenValidator,
    BreakResultValidator breakResultValidator,
    IClock clock)
{
    public async Task<ServiceResult<Specimen>> AddAsync(
        int batchId,
        Specimen specimen)
    {
        var batch = await specimenRepository.GetBatchAsync(batchId);

        if (batch is null)
        {
            return ServiceResult<Specimen>.NotFound(
            [
                new ValidationError(
                    "Specimen.BatchId",
                    "Batch was not found.")
            ]);
        }

        var errors = new List<ValidationError>();

        specimen.Label = specimen.Label.Trim();

        errors.AddRange(specimenValidator.Validate(specimen));

        if (string.IsNullOrWhiteSpace(specimen.Label))
        {
            errors.Add(new ValidationError(
                "Specimen.Label",
                "Specimen label is required."));
        }
        else if (specimen.Label.Length > 20)
        {
            errors.Add(new ValidationError(
                "Specimen.Label",
                "Specimen label cannot exceed 20 characters."));
        }

        if (errors.Count > 0)
        {
            return ServiceResult<Specimen>.Invalid(errors);
        }

        if (await specimenRepository.LabelExistsAsync(
                batchId,
                specimen.Label))
        {
            return ServiceResult<Specimen>.Conflict(
            [
                new ValidationError(
                    "Specimen.Label",
                    "Specimen label already exists for this batch.")
            ]);
        }

        specimen.BatchId = batchId;

        await specimenRepository.AddAsync(specimen);
        await specimenRepository.SaveChangesAsync();

        return ServiceResult<Specimen>.Success(specimen);
    }

    public async Task<ServiceResult<BreakResult>> RecordBreakAsync(
        int specimenId,
        BreakResult breakResult)
    {
        var specimen =
            await specimenRepository.GetByIdAsync(specimenId);

        if (specimen is null)
        {
            return ServiceResult<BreakResult>.NotFound(
            [
                new ValidationError(
                    "Break.SpecimenId",
                    "Specimen was not found.")
            ]);
        }

        if (specimen.BreakResult is not null)
        {
            return ServiceResult<BreakResult>.Conflict(
            [
                new ValidationError(
                    "Break.SpecimenId",
                    "This specimen already has a break result.")
            ]);
        }

        var validation = breakResultValidator.Validate(
            breakResult,
            specimen.Batch.CastAt,
            false,
            clock.UtcNow);

        if (validation.Errors.Count > 0)
        {
            return ServiceResult<BreakResult>.Invalid(
                validation.Errors);
        }

        breakResult.SpecimenId = specimenId;

        await specimenRepository.AddBreakResultWithWarningsAsync(
            breakResult,
            breakResultId =>
                validation.Warnings.Select(warning => new RecordWarning
                {
                    EntityType = nameof(BreakResult),
                    EntityId = breakResultId,
                    FieldName = warning.Key,
                    LimitKey = warning.Key,
                    Value = breakResult.StrengthPsi,
                    Message = warning.Message
                }).ToList());

        return ServiceResult<BreakResult>.Success(
            breakResult,
            validation.Warnings);
    }
}
