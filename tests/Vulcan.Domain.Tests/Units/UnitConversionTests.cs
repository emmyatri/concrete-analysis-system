using Shouldly;
using Vulcan.Domain.Units;

namespace Vulcan.Domain.Tests.Units;

public class UnitConversionsTests
{
    // Verify known freezing-point conversions in both directions.
    [Fact]
    public void FahrenheitToCelsius_FreezingPoint_ReturnsZero()
    {
        var result = UnitConversions.FahrenheitToCelsius(32m);

        result.ShouldBe(0m);
    }

    [Fact]
    public void CelsiusToFahrenheit_FreezingPoint_ReturnsThirtyTwo()
    {
        var result = UnitConversions.CelsiusToFahrenheit(0m);

        result.ShouldBe(32m);
    }

    // -40 is the point where the Fahrenheit and Celsius scales are equal.
    // These tests verify that negative temperatures convert correctly.
    [Fact]
    public void FahrenheitToCelsius_NegativeForty_ReturnsNegativeForty()
    {
        var result = UnitConversions.FahrenheitToCelsius(-40m);

        result.ShouldBe(-40m);
    }

    [Fact]
    public void CelsiusToFahrenheit_NegativeForty_ReturnsNegativeForty()
    {
        var result = UnitConversions.CelsiusToFahrenheit(-40m);

        result.ShouldBe(-40m);
    }

    // Verify conversion at the physical lower boundary of temperature.
    [Fact]
    public void FahrenheitToCelsius_AbsoluteZero_ReturnsExpectedValue()
    {
        var result = UnitConversions.FahrenheitToCelsius(-459.67m);

        result.ShouldBe(-273.15m, 0.0001m);
    }

    [Fact]
    public void CelsiusToFahrenheit_AbsoluteZero_ReturnsExpectedValue()
    {
        var result = UnitConversions.CelsiusToFahrenheit(-273.15m);

        result.ShouldBe(-459.67m, 0.0001m);
    }

    // Verify that decimal inputs retain sufficient precision during conversion.
    [Fact]
    public void FahrenheitToCelsius_DecimalValue_ReturnsExpectedValue()
    {
        var result = UnitConversions.FahrenheitToCelsius(98.6m);

        result.ShouldBe(37m, 0.0001m);
    }

    // Convert Fahrenheit to Celsius and back to verify that the original
    // canonical value is preserved through a round-trip conversion.
    [Theory]
    [InlineData(32)]
    [InlineData(72)]
    [InlineData(100)]
    [InlineData(150)]
    [InlineData(0)]
    public void FahrenheitToCelsiusAndBack_ReturnsOriginalValue(int fahrenheit)
    {
        var original = (decimal)fahrenheit;

        var celsius = UnitConversions.FahrenheitToCelsius(original);
        var result = UnitConversions.CelsiusToFahrenheit(celsius);

        result.ShouldBe(original, 0.0001m);
    }

    // Perform the same round-trip check starting from Celsius to ensure
    // conversion precision is maintained in both directions.
    [Theory]
    [InlineData(-273)]
    [InlineData(-40)]
    [InlineData(0)]
    [InlineData(37)]
    [InlineData(100)]
    public void CelsiusToFahrenheitAndBack_ReturnsOriginalValue(int celsius)
    {
        var original = (decimal)celsius;

        var fahrenheit = UnitConversions.CelsiusToFahrenheit(original);
        var result = UnitConversions.FahrenheitToCelsius(fahrenheit);

        result.ShouldBe(original, 0.0001m);
    }
}
