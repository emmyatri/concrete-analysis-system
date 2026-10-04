using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Validation;

namespace Vulcan.Domain.Services;

/// <summary>
/// The rules for one curing reading on a cylinder (M1-16, FR-6).
/// Ranges come from the ValidationLimit table, so no numbers live here.
/// </summary>
public class CuringReadingValidator(LimitEvaluator limitEvaluator)
{
    private const string _takenAtKey = "Curing.TakenAt";

    /// <summary>
    /// Checks every field and returns all problems at once.
    /// Errors block the save. Warnings (e.g. ambient temperature outside 60–80 °F) save and are flagged.
    /// </summary>
    /// <param name="reading">The reading to check.</param>
    /// <param name="batchCastAt">When the cylinder's batch was poured (UTC).</param>
    /// <param name="nowUtc">The current time (UTC), passed in so tests can fix it.</param>
    public ValidationOutcome Validate(CuringReading reading, DateTime batchCastAt, DateTime nowUtc)
    {
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationError>();

        if (reading.TakenAt == default)
        {
            errors.Add(new ValidationError(_takenAtKey, "Enter when the reading was taken."));
        }
        else if (reading.TakenAt < batchCastAt)
        {
            errors.Add(new ValidationError(_takenAtKey, "Reading time can't be before the batch was cast."));
        }
        else if (reading.TakenAt > nowUtc)
        {
            errors.Add(new ValidationError(_takenAtKey, "Reading time can't be in the future."));
        }

        Check(LimitKeys.InternalTempF, reading.InternalTempF, errors, warnings);
        Check(LimitKeys.AmbientTempF, reading.AmbientTempF, errors, warnings);
        Check(LimitKeys.MoisturePct, reading.MoisturePct, errors, warnings);
        Check(LimitKeys.RelativeHumidityPct, reading.RelativeHumidityPct, errors, warnings);
        Check(LimitKeys.ResistivityOhmPerFt, reading.ResistivityOhmPerFt, errors, warnings);
        Check(LimitKeys.Co2Ppm, reading.Co2Ppm, errors, warnings);

        return new ValidationOutcome(errors, warnings);
    }

    private void Check(
        string key,
        decimal value,
        List<ValidationError> errors,
        List<ValidationError> warnings)
    {
        switch (limitEvaluator.Check(key, value))
        {
            case LimitResult.Reject reject:
                errors.Add(new ValidationError(key, reject.Message));
                break;
            case LimitResult.Warn warn:
                warnings.Add(new ValidationError(key, warn.Message));
                break;
        }
    }
}
