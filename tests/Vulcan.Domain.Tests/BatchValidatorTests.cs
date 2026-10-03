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
    // Extra whitespace is removed before validation.
    [Fact]
    public void BatchNumber_IsRequired()
    {
        Assert.False(BatchValidator.IsBatchNumberValid(null));
        Assert.False(BatchValidator.IsBatchNumberValid(""));
        Assert.False(BatchValidator.IsBatchNumberValid("   "));
    }

    // Extra whitespace is removed from anywhere in the batch number before validation.
    [Theory]
    [InlineData("  VM-123  ", "VM-123")]
    [InlineData("VM 123", "VM123")]
    [InlineData(" VM 12 3 ", "VM123")]
    public void BatchNumber_RemovesExtraWhitespace(
        string batchNumber,
        string expected)
    {
        var result = BatchValidator.NormalizeBatchNumber(batchNumber);

        Assert.Equal(expected, result);
        Assert.True(BatchValidator.IsBatchNumberValid(result));
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

    // Final weight must be greater than zero and contain no more than
    // two decimal places. The numeric range comes from the configured limit.
    [Theory]
    [InlineData("0.01")]
    [InlineData("1")]
    [InlineData("1.1")]
    [InlineData("1.12")]
    [InlineData("100.00")]
    public void FinalWeight_WithValidValue_IsValid(string value)
    {
        var finalWeightLb = decimal.Parse(value);

        Assert.True(_validator.IsFinalWeightValid(finalWeightLb));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    [InlineData("1.001")]
    [InlineData("1.123")]
    public void FinalWeight_WithInvalidValue_IsInvalid(string value)
    {
        var finalWeightLb = decimal.Parse(value);

        Assert.False(_validator.IsFinalWeightValid(finalWeightLb));
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
                        Min = 0.01m,
                        Max = null,
                        Unit = "lb",
                        Message = "Weight must be greater than 0",
                        Source = "schema.md"
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
