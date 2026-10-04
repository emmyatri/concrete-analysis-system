using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Validation;

namespace Vulcan.Domain.Services;

public class SpecimenValidator(LimitEvaluator limitEvaluator)
{
    private const string _testAgeDaysKey = "Specimen.TestAgeDays";

    public bool IsLengthValid(decimal lengthIn)
    {
        return limitEvaluator.Check(
            LimitKeys.LengthIn,
            lengthIn) is not LimitResult.Reject;
    }

    public bool IsWidthValid(decimal widthIn)
    {
        return limitEvaluator.Check(
            LimitKeys.WidthIn,
            widthIn) is not LimitResult.Reject;
    }

    public static bool HasAtMostTwoDecimalPlaces(decimal value)
    {
        return decimal.Round(value, 2) == value;
    }

    public static bool IsTestAgeValid(TestAgeDays testAgeDays)
    {
        return testAgeDays is TestAgeDays.Day3
            or TestAgeDays.Day7
            or TestAgeDays.Day28;
    }

    public IReadOnlyList<ValidationError> Validate(Specimen specimen)
    {
        var errors = new List<ValidationError>();

        AddLimitError(errors, LimitKeys.LengthIn, specimen.LengthIn);
        AddLimitError(errors, LimitKeys.WidthIn, specimen.WidthIn);

        if (!HasAtMostTwoDecimalPlaces(specimen.LengthIn))
        {
            errors.Add(new ValidationError(
                LimitKeys.LengthIn,
                "Specimen length cannot have more than two decimal places."));
        }

        if (!HasAtMostTwoDecimalPlaces(specimen.WidthIn))
        {
            errors.Add(new ValidationError(
                LimitKeys.WidthIn,
                "Specimen width cannot have more than two decimal places."));
        }

        if (!IsTestAgeValid(specimen.TestAgeDays))
        {
            errors.Add(new ValidationError(
                _testAgeDaysKey,
                "Test age must be 3, 7, or 28 days."));
        }

        return errors;
    }

    private void AddLimitError(
        List<ValidationError> errors,
        string key,
        decimal value)
    {
        if (limitEvaluator.Check(key, value) is LimitResult.Reject reject)
        {
            errors.Add(new ValidationError(
                key,
                reject.Message));
        }
    }
}
