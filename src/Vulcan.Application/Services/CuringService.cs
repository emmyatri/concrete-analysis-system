using Vulcan.Application.Interfaces;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Services;
using Vulcan.Domain.Validation;

namespace Vulcan.Application.Services;

public sealed class CuringService(
    ICuringRepository curingRepository,
    CuringReadingValidator curingReadingValidator,
    IClock clock)
{
    public async Task<ServiceResult<CuringReading>> AddReadingAsync(
        int specimenId,
        CuringReading reading)
    {
        var specimen =
            await curingRepository.GetSpecimenAsync(specimenId);

        if (specimen is null)
        {
            return ServiceResult<CuringReading>.Failure(
            [
                new ValidationError(
                    "Curing.SpecimenId",
                    "Specimen was not found.")
            ]);
        }

        var validation = curingReadingValidator.Validate(
            reading,
            specimen.Batch.CastAt,
            clock.UtcNow);

        if (validation.Errors.Count > 0)
        {
            return ServiceResult<CuringReading>.Failure(
                validation.Errors);
        }

        reading.SpecimenId = specimenId;

        await curingRepository.AddAsync(reading);
        await curingRepository.SaveChangesAsync();

        return ServiceResult<CuringReading>.Success(reading);
    }
}
