namespace Vulcan.Prediction.Maturity;

public sealed record MaturitySettings
{
    public double DatumTempF { get; init; } = 32.0;

    public double ReferenceTempF { get; init; } = 73.4;
}
