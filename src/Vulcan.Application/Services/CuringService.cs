using Vulcan.Application.Interfaces;
using Vulcan.Domain.Constants;
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
            return ServiceResult<CuringReading>.NotFound(
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
            return ServiceResult<CuringReading>.Invalid(
                validation.Errors);
        }

        reading.SpecimenId = specimenId;

        await curingRepository.AddWithWarningsAsync(
            reading,
            readingId =>
                validation.Warnings.Select(warning => new RecordWarning
                {
                    EntityType = nameof(CuringReading),
                    EntityId = readingId,
                    FieldName = warning.Key,
                    LimitKey = warning.Key,
                    Value = warning.Key switch
                    {
                        LimitKeys.InternalTempF => reading.InternalTempF,
                        LimitKeys.AmbientTempF => reading.AmbientTempF,
                        LimitKeys.MoisturePct => reading.MoisturePct,
                        LimitKeys.RelativeHumidityPct => reading.RelativeHumidityPct,
                        LimitKeys.ResistivityOhmPerFt => reading.ResistivityOhmPerFt,
                        LimitKeys.Co2Ppm => reading.Co2Ppm,
                        _ => throw new InvalidOperationException(
                            $"Unsupported curing warning key '{warning.Key}'.")
                    },
                    Message = warning.Message
                }).ToList());

        return ServiceResult<CuringReading>.Success(
            reading,
            validation.Warnings);
    }
}
