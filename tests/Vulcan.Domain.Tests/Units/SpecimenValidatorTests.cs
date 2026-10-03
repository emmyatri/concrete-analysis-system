using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Interfaces;
using Vulcan.Domain.Services;
using Vulcan.Domain.Validation;
using static Vulcan.Infrastructure.SetupUtils;

namespace Vulcan.Domain.Tests;

public class SpecimenValidatorTests
{
    private readonly SpecimenValidator _validator;

    public SpecimenValidatorTests()
    {
        // Use the application's M1-10 seeded limits so specimen range values
        // remain centralized instead of being duplicated in the validator.
        _validator = new SpecimenValidator(
            new LimitEvaluator(
                new FakeLimitProvider([.. LoadValidationLimits()])));
    }

    // -------------------------------------------------------------------------
    // Length
    // The configured maximum is inclusive. Zero and values above the maximum
    // must be rejected.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("0.01", true)]
    [InlineData("12.00", true)]
    [InlineData("0", false)]
    [InlineData("12.01", false)]
    public void Length_Range_IsValidated(string value, bool expected)
    {
        Assert.Equal(
            expected,
            _validator.IsLengthValid(decimal.Parse(value)));
    }

    // -------------------------------------------------------------------------
    // Width
    // The configured maximum is inclusive. Zero and values above the maximum
    // must be rejected.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("0.01", true)]
    [InlineData("6.00", true)]
    [InlineData("0", false)]
    [InlineData("6.01", false)]
    public void Width_Range_IsValidated(string value, bool expected)
    {
        Assert.Equal(
            expected,
            _validator.IsWidthValid(decimal.Parse(value)));
    }

    // -------------------------------------------------------------------------
    // Decimal precision
    // Length and width may contain at most two decimal places. Values are
    // rejected rather than rounded when additional precision is supplied.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("4")]
    [InlineData("4.0")]
    [InlineData("4.00")]
    [InlineData("12.00")]
    public void Measurement_WithAtMostTwoDecimalPlaces_IsValid(string value)
    {
        Assert.True(
            SpecimenValidator.HasAtMostTwoDecimalPlaces(
                decimal.Parse(value)));
    }

    [Theory]
    [InlineData("4.005")]
    [InlineData("6.001")]
    [InlineData("11.999")]
    public void Measurement_WithMoreThanTwoDecimalPlaces_IsInvalid(string value)
    {
        Assert.False(
            SpecimenValidator.HasAtMostTwoDecimalPlaces(
                decimal.Parse(value)));
    }

    [Fact]
    public void Validate_WithExcessPrecision_ReturnsPrecisionMessage()
    {
        var specimen = CreateSpecimen(
            lengthIn: 4.005m,
            widthIn: 4m,
            testAgeDays: TestAgeDays.Day7);

        var errors = _validator.Validate(specimen);

        Assert.Contains(
            "Specimen length cannot have more than two decimal places.",
            errors);
    }

    // -------------------------------------------------------------------------
    // Test age
    // Only the three scheduled break ages represented by the domain enum
    // are accepted.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(TestAgeDays.Day3)]
    [InlineData(TestAgeDays.Day7)]
    [InlineData(TestAgeDays.Day28)]
    public void TestAge_SupportedValue_IsValid(TestAgeDays testAgeDays)
    {
        Assert.True(
            SpecimenValidator.IsTestAgeValid(testAgeDays));
    }

    [Fact]
    public void TestAge_UnsupportedValue_IsInvalid()
    {
        Assert.False(
            SpecimenValidator.IsTestAgeValid((TestAgeDays)10));
    }

    // -------------------------------------------------------------------------
    // Complete validation
    // A valid specimen returns no errors. Multiple invalid fields report all
    // applicable problems instead of stopping after the first failure.
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_WithValidSpecimen_ReturnsNoErrors()
    {
        var specimen = CreateSpecimen(
            lengthIn: 12.00m,
            widthIn: 6.00m,
            testAgeDays: TestAgeDays.Day28);

        var errors = _validator.Validate(specimen);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WithMultipleProblems_ReturnsAllErrors()
    {
        var specimen = CreateSpecimen(
            lengthIn: 12.01m,
            widthIn: 6.01m,
            testAgeDays: (TestAgeDays)10);

        var errors = _validator.Validate(specimen);

        Assert.Contains(
            "Specimen length must be nonzero and in the range [0-12]",
            errors);

        Assert.Contains(
            "Specimen width must be nonzero and in the range [0-6]",
            errors);

        Assert.Contains(
            "Test age must be 3, 7, or 28 days.",
            errors);
    }

    // -------------------------------------------------------------------------
    // Test helpers
    // -------------------------------------------------------------------------

    private static Specimen CreateSpecimen(
        decimal lengthIn,
        decimal widthIn,
        TestAgeDays testAgeDays)
    {
        return new Specimen
        {
            LengthIn = lengthIn,
            WidthIn = widthIn,
            TestAgeDays = testAgeDays
        };
    }

    // Lightweight provider supplies the real M1-10 seed limits to
    // LimitEvaluator without requiring a database for these unit tests.
    private sealed class FakeLimitProvider(params ValidationLimit[] limits)
        : ILimitProvider
    {
        public LimitSet Get(string key)
        {
            var matches = limits
                .Where(limit => limit.Key == key)
                .ToList();

            if (matches.Count == 0)
            {
                throw new KeyNotFoundException(key);
            }

            return new LimitSet
            {
                Warn = matches.FirstOrDefault(
                    limit => limit.Severity == Severity.Warn),

                Reject = matches.FirstOrDefault(
                    limit => limit.Severity == Severity.Reject)
            };
        }
    }
}
