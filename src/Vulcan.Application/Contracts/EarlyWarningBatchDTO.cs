namespace Vulcan.Application.Contracts;

public sealed record EarlyWarningBatchDTO(
    int Id,
    string BatchNumber,
    bool IsSynthetic,
    bool AtRisk,
    double? Projected28DayPsi,
    double? ShortfallPsi,
    double? ShortfallPct,
    IReadOnlyList<EarlyWarningDTO> EarlyWarnings);
