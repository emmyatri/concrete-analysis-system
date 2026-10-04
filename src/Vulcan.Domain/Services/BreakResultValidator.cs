using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Validation;

namespace Vulcan.Domain.Services;

public class BreakResultValidator(LimitEvaluator limitEvaluator)
{
    private const string _brokenAtKey = "Break.BrokenAt";
    private const string _specimenIdKey = "Break.SpecimenId";

    public ValidationOutcome Validate(
        BreakResult breakResult,
        DateTime castAt,
        bool specimenAlreadyHasResult,
        DateTime now)
    {
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationError>();

        var strengthResult = limitEvaluator.Check(
            LimitKeys.StrengthPsi,
            breakResult.StrengthPsi);

        switch (strengthResult)
        {
            case LimitResult.Reject reject:
                errors.Add(new ValidationError(
                    LimitKeys.StrengthPsi,
                    reject.Message));
                break;

            case LimitResult.Warn warn:
                warnings.Add(new ValidationError(
                    LimitKeys.StrengthPsi,
                    warn.Message));
                break;
        }

        if (specimenAlreadyHasResult)
        {
            errors.Add(new ValidationError(
                _specimenIdKey,
                "This specimen already has a break result."));
        }

        if (breakResult.BrokenAt < castAt)
        {
            errors.Add(new ValidationError(
                _brokenAtKey,
                "Break time cannot be before the batch cast time."));
        }

        if (breakResult.BrokenAt > now)
        {
            errors.Add(new ValidationError(
                _brokenAtKey,
                "Break time cannot be in the future."));
        }

        return new ValidationOutcome(errors, warnings);
    }
}
