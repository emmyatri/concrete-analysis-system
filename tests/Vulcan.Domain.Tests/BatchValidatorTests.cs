using Vulcan.Domain.Services;

namespace Vulcan.Domain.Tests;

public class BatchValidatorTests
{
    [Fact]
    public void BatchNumber_IsRequired()
    {
        Assert.False(BatchValidator.IsBatchNumberValid(null));
        Assert.False(BatchValidator.IsBatchNumberValid(""));
        Assert.False(BatchValidator.IsBatchNumberValid("   "));
    }

    [Fact]
    public void BatchNumber_RemovesLeadingAndTrailingSpaces()
    {
        var result = BatchValidator.NormalizeBatchNumber("  VM-123  ");

        Assert.Equal("VM-123", result);
        Assert.True(BatchValidator.IsBatchNumberValid("  VM-123  "));
    }

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
    [InlineData("ABC 123")]
    [InlineData("ABC_123")]
    [InlineData("ABC.123")]
    [InlineData("ABC@123")]
    [InlineData("ABC/123")]
    public void BatchNumber_WithInvalidCharacters_IsInvalid(string batchNumber)
    {
        Assert.False(BatchValidator.IsBatchNumberValid(batchNumber));
    }
    
    [Theory]
    [InlineData("0.01")]
    [InlineData("1")]
    [InlineData("1.1")]
    [InlineData("1.12")]
    [InlineData("100.00")]
    public void FinalWeight_WithValidValue_IsValid(string value)
    {
        var finalWeightLb = decimal.Parse(value);

        Assert.True(BatchValidator.IsFinalWeightValid(finalWeightLb));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    [InlineData("1.001")]
    [InlineData("1.123")]
    public void FinalWeight_WithInvalidValue_IsInvalid(string value)
    {
        var finalWeightLb = decimal.Parse(value);

        Assert.False(BatchValidator.IsFinalWeightValid(finalWeightLb));
    }
}
