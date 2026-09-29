namespace Vulcan.Domain.Units;

public static class UnitConversions
{
    public static decimal FahrenheitToCelsius(decimal fahrenheit)
    {
        return (fahrenheit - 32m) * 5m / 9m;
    }

    public static decimal CelsiusToFahrenheit(decimal celsius)
    {
        return celsius * 9m / 5m + 32m;
    }
}
