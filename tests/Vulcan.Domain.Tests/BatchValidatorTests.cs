using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Interfaces;
using Vulcan.Domain.Services;
using Vulcan.Domain.Validation;

namespace Vulcan.Domain.Tests;

public class BatchValidatorTests
{
    private readonly BatchValidator _validator;

    public BatchValidatorTests()
    {
        var limitProvider = new TestLimitProvider();
        var limitEvaluator = new LimitEvaluator(limitProvider);
        _validator = new BatchValidator(limitEvaluator);
    }

    // Batch numbers are required and may contain only letters, numbers, and dashes.
    // Leading and trailing whitespace is removed before validation.
    [Fact]
    public void BatchNumber_IsRequired()
    {
        Assert.False(BatchValidator.IsBatchNumberValid(null));
        Assert.False(BatchValidator.IsBatchNumberValid(""));
        Assert.False(BatchValidator.IsBatchNumberValid("   "));
    }

    // Leading and trailing whitespace is removed before validation.
    [Theory]
    [InlineData("  VM-123  ", "VM-123")]
    [InlineData("\tVM-123\t", "VM-123")]
    public void BatchNumber_RemovesLeadingAndTrailingWhitespace(
        string batchNumber,
        string expected)
    {
        var result = BatchValidator.NormalizeBatchNumber(batchNumber);

        Assert.Equal(expected, result);
        Assert.True(BatchValidator.IsBatchNumberValid(result));
    }

    // Whitespace inside a batch number is not allowed.
    [Theory]
    [InlineData("VM 123")]
    [InlineData("VM 12 3")]
    public void BatchNumber_WithInnerWhitespace_IsInvalid(string batchNumber)
    {
        Assert.False(BatchValidator.IsBatchNumberValid(batchNumber));
    }

    // Batch numbers have a maximum length of 32 characters.
    [Fact]
    public void BatchNumber_With32Characters_IsValid()
    {
        var batchNumber = new string('A', 32);

        Assert.True(BatchValidator.IsBatchNumberValid(batchNumber));
    }

    [Fact]
    public void BatchNumber_With33Characters_IsInvalid()
    {
        var batchNumber = new string('A', 33);

        Assert.False(BatchValidator.IsBatchNumberValid(batchNumber));
    }

    [Theory]
    [InlineData("ABC-123")]
    [InlineData("abc-123")]
    [InlineData("A1")]
    [InlineData("123456")]
    [InlineData("A-B-C-123")]
    public void BatchNumber_WithAllowedCharacters_IsValid(string batchNumber)
    {
        Assert.True(BatchValidator.IsBatchNumberValid(batchNumber));
    }

    [Theory]
    [InlineData("ABC_123")]
    [InlineData("ABC.123")]
    [InlineData("ABC@123")]
    [InlineData("ABC/123")]
    public void BatchNumber_WithInvalidCharacters_IsInvalid(string batchNumber)
    {
        Assert.False(BatchValidator.IsBatchNumberValid(batchNumber));
    }

    // Final weight must be between 10 and 40 and must be within
    // two decimal places. The numeric range comes from the configured limit.
    [Theory]
    [InlineData("10.0")]
    [InlineData("10.1")]
    [InlineData("39.9")]
    [InlineData("40.0")]
    public void FinalWeight_WithValidValue_IsValid(string value)
    {
        var finalWeightLb = decimal.Parse(value);

#pragma warning disable CS0618 // Type or member is obsolete
        Assert.True(_validator.IsFinalWeightValid(finalWeightLb));
#pragma warning restore CS0618 // Type or member is obsolete
    }

    [Theory]
    [InlineData("9.99")]
    [InlineData("40.01")]
    [InlineData("25.001")]
    [InlineData("25.123")]
    public void FinalWeight_WithInvalidValue_IsInvalid(string value)
    {
        var finalWeightLb = decimal.Parse(value);
#pragma warning disable CS0618 // Type or member is obsolete
        Assert.False(_validator.IsFinalWeightValid(finalWeightLb));
#pragma warning restore CS0618 // Type or member is obsolete
    }

    [Theory]
    [InlineData("20.0")]
    [InlineData("25.0")]
    [InlineData("30.0")]
    public void FinalWeightCheck_WithValidValue_IsValid(string value)
    {
        var finalWeightLb = decimal.Parse(value);

        var result = _validator.CheckFinalWeightLb(finalWeightLb);
        result.ShouldBeOfType<LimitResult.Ok>();
    }

    [Theory]
    [InlineData("10.00")]
    [InlineData("10.01")]
    [InlineData("19.99")]
    [InlineData("30.01")]
    [InlineData("35.00")]
    [InlineData("40.00")]
    public void FinalWeightCheck_WithWarningValue_IsWarn(string value)
    {
        var finalWeightLb = decimal.Parse(value);

        var result = _validator.CheckFinalWeightLb(finalWeightLb);
        result
            .ShouldBeOfType<LimitResult.Warn>()
            .Message
            .ShouldBe("Sample weight is outside the normal range [20-30] for a 12x6 cylinder");
    }

    [Theory]
    [InlineData("9.99", "Sample weight must be within the range [10-40]")]
    [InlineData("40.01", "Sample weight must be within the range [10-40]")]
    [InlineData("35.1202", "Final weight must be rounded to two decimal places.")]
    [InlineData("25.391", "Final weight must be rounded to two decimal places.")]
    public void FinalWeightCheck_WithRejectValue_IsReject(string value, string message)
    {
        var finalWeightLb = decimal.Parse(value);

        var result = _validator.CheckFinalWeightLb(finalWeightLb);
        result
            .ShouldBeOfType<LimitResult.Reject>()
            .Message
            .ShouldBe(message);
    }

    // Cast time is required and cannot be later than the current time.
    // A fixed UTC time keeps the boundary tests deterministic.
    [Fact]
    public void CastAt_IsRequired()
    {
        var nowUtc = new DateTime(2026, 10, 3, 18, 0, 0, DateTimeKind.Utc);

        Assert.False(BatchValidator.IsCastAtValid(default, nowUtc));
    }

    [Fact]
    public void CastAt_AtCurrentTime_IsValid()
    {
        var nowUtc = new DateTime(2026, 10, 3, 18, 0, 0, DateTimeKind.Utc);

        Assert.True(BatchValidator.IsCastAtValid(nowUtc, nowUtc));
    }

    [Fact]
    public void CastAt_JustBeforeCurrentTime_IsValid()
    {
        var nowUtc = new DateTime(2026, 10, 3, 18, 0, 0, DateTimeKind.Utc);
        var castAt = nowUtc.AddTicks(-1);

        Assert.True(BatchValidator.IsCastAtValid(castAt, nowUtc));
    }

    [Fact]
    public void CastAt_JustAfterCurrentTime_IsInvalid()
    {
        var nowUtc = new DateTime(2026, 10, 3, 18, 0, 0, DateTimeKind.Utc);
        var castAt = nowUtc.AddTicks(1);

        Assert.False(BatchValidator.IsCastAtValid(castAt, nowUtc));
    }

    // Cement type is required and must be one of the three supported types.
    [Theory]
    [InlineData(CementType.TypeI)]
    [InlineData(CementType.TypeII)]
    [InlineData(CementType.TypeIII)]
    public void CementType_WithSupportedType_IsValid(CementType cementType)
    {
        Assert.True(BatchValidator.IsCementTypeValid(cementType));
    }

    [Fact]
    public void CementType_WithDefaultValue_IsInvalid()
    {
        Assert.False(BatchValidator.IsCementTypeValid(default));
    }

    [Fact]
    public void CementType_WithUnsupportedValue_IsInvalid()
    {
        var cementType = (CementType)4;

        Assert.False(BatchValidator.IsCementTypeValid(cementType));
    }

    // Expected final break strength must be between 3,000 and 10,000 PSI,
    // inclusive. The numeric range comes from the configured limit.
    [Theory]
    [InlineData(3000)]
    [InlineData(10000)]
    public void ExpectedFinalBreakPsi_AtBoundary_IsValid(int value)
    {
        Assert.True(_validator.IsExpectedFinalBreakPsiValid(value));
    }

    [Theory]
    [InlineData(2999)]
    [InlineData(10001)]
    public void ExpectedFinalBreakPsi_OutsideBoundary_IsInvalid(int value)
    {
        Assert.False(_validator.IsExpectedFinalBreakPsiValid(value));
    }

    private sealed class TestLimitProvider : ILimitProvider
    {
        public LimitSet Get(string key)
        {
            return key switch
            {
                LimitKeys.FinalWeightLb => new LimitSet
                {
                    Reject = new ValidationLimit
                    {
                        Key = LimitKeys.FinalWeightLb,
                        Severity = Severity.Reject,
                        Min = 10,
                        Max = 40,
                        Unit = "lb",
                        Message = "Sample weight must be within the range [10-40]",
                        Source = "Client, M1 demo (Oct 9)"
                    },
                    Warn = new ValidationLimit
                    {
                        Key = LimitKeys.FinalWeightLb,
                        Severity = Severity.Warn,
                        Min = 20,
                        Max = 30,
                        Unit = "lb",
                        Message = "Sample weight is outside the normal range [20-30] for a 12x6 cylinder",
                        Source = "Client, M1 demo (Oct 9)"
                    }
                },

                LimitKeys.ExpectedFinalBreakPsi => new LimitSet
                {
                    Reject = new ValidationLimit
                    {
                        Key = LimitKeys.ExpectedFinalBreakPsi,
                        Severity = Severity.Reject,
                        Min = 3000m,
                        Max = 10000m,
                        Unit = "PSI",
                        Message = "PSI must be within the range [3000-10,000]",
                        Source = "Client doc / FR-5"
                    }
                },

                _ => new LimitSet()
            };
        }
    }


}
