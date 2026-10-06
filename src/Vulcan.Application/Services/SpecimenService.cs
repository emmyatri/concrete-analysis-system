using Vulcan.Application.Interfaces;
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
        var errors = new List<ValidationError>();

        var batch = await specimenRepository.GetBatchAsync(batchId);

        if (batch is null)
        {
            errors.Add(new ValidationError(
                "Specimen.BatchId",
                "Batch was not found."));

            return ServiceResult<Specimen>.Failure(errors);
        }

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
        else if (await specimenRepository.LabelExistsAsync(
                     batchId,
                     specimen.Label))
        {
            errors.Add(new ValidationError(
                "Specimen.Label",
                "Specimen label already exists for this batch."));
        }

        if (errors.Count > 0)
        {
            return ServiceResult<Specimen>.Failure(errors);
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
            return ServiceResult<BreakResult>.Failure(
            [
                new ValidationError(
                    "Break.SpecimenId",
                    "Specimen was not found.")
            ]);
        }

        var validation = breakResultValidator.Validate(
            breakResult,
            specimen.Batch.CastAt,
            specimen.BreakResult is not null,
            clock.UtcNow);

        if (validation.Errors.Count > 0)
        {
            return ServiceResult<BreakResult>.Failure(
                validation.Errors);
        }

        breakResult.SpecimenId = specimenId;

        await specimenRepository.AddBreakResultAsync(breakResult);
        await specimenRepository.SaveChangesAsync();

        return ServiceResult<BreakResult>.Success(breakResult);
    }
}
