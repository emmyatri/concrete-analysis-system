namespace Vulcan.Application.Contracts;

public sealed record EarlyWarningEvaluation(
    bool AtRisk,
    double? Projected28DayPsi,
    double? ShortfallPsi,
    double? ShortfallPct,
    IReadOnlyList<EarlyWarningDTO> EarlyWarnings);
