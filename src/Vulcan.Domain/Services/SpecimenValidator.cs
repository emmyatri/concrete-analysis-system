using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Validation;

namespace Vulcan.Domain.Services;

public class SpecimenValidator(LimitEvaluator limitEvaluator)
{
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

    public IReadOnlyList<string> Validate(Specimen specimen)
    {
        var errors = new List<string>();

        AddLimitError(errors, LimitKeys.LengthIn, specimen.LengthIn);
        AddLimitError(errors, LimitKeys.WidthIn, specimen.WidthIn);

        if (!HasAtMostTwoDecimalPlaces(specimen.LengthIn))
        {
            errors.Add(
                "Specimen length cannot have more than two decimal places.");
        }

        if (!HasAtMostTwoDecimalPlaces(specimen.WidthIn))
        {
            errors.Add(
                "Specimen width cannot have more than two decimal places.");
        }

        if (!IsTestAgeValid(specimen.TestAgeDays))
        {
            errors.Add(
                "Test age must be 3, 7, or 28 days.");
        }

        return errors;
    }

    private void AddLimitError(
        List<string> errors,
        string key,
        decimal value)
    {
        if (limitEvaluator.Check(key, value) is LimitResult.Reject reject)
        {
            errors.Add(reject.Message);
        }
    }
}
