using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
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
        DateTime now,
        TestAgeDays testAgeDays = TestAgeDays.Day28)
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

            // The 3,000 PSI warning applies to 28-day breaks only. Earlier breaks are expected to be
            // lower (client-decisions §3, team proposal pending client confirmation).
            case LimitResult.Warn warn when testAgeDays == TestAgeDays.Day28:
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
