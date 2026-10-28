namespace Vulcan.Application.Services.EarlyWarning;

public class EarlyWarningResult
{
    public bool AtRisk { get; set; }
    public int? ShortfallPsi { get; set; }
    public double? ShortfallPct { get; set; }
    public required List<EarlyWarning> EarlyWarnings { get; set; }
}
